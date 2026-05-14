using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;
using WebApplication1.Api.Parsing;
using WebApplication1.Api.Middleware;
using WebApplication1.Application;
using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.Configuration;
using WebApplication1.Infrastructure.Messaging;

namespace WebApplication1.Api.Controllers
{
    /// <summary>
    /// Controller de Webhooks — responsável APENAS por receber HTTP e fazer parse.
    /// 
    /// Toda a lógica de negócio vive no MessageProcessingService.
    /// Este controller é "magro" — HTTP entry point apenas.
    /// </summary>
    [Route("api/webhook")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly WebhookConcurrencyGuard _concurrencyGuard;
        private readonly ILogger<WebhookController> _logger;
        private readonly WhatsAppSettings _whatsAppSettings;
        private readonly TeamsSettings _teamsSettings;

        public WebhookController(
            IServiceScopeFactory scopeFactory,
            WebhookConcurrencyGuard concurrencyGuard,
            IOptions<WhatsAppSettings> whatsAppSettings,
            IOptions<TeamsSettings> teamsSettings,
            ILogger<WebhookController> logger)
        {
            _scopeFactory = scopeFactory;
            _concurrencyGuard = concurrencyGuard;
            _logger = logger;
            _whatsAppSettings = whatsAppSettings.Value;
            _teamsSettings = teamsSettings.Value;
        }

        /// <summary>
        /// GET — Verificação do Webhook WhatsApp (handshake com a Meta).
        /// </summary>
        [HttpGet("whatsapp")]
        [EnableRateLimiting("webhook")]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string? hubMode,
            [FromQuery(Name = "hub.verify_token")] string? hubVerifyToken,
            [FromQuery(Name = "hub.challenge")] string? hubChallenge)
        {
            bool tokenValid = hubVerifyToken == _whatsAppSettings.VerifyToken;
            bool success = hubMode == "subscribe" && tokenValid;

            if (success)
                return Ok(hubChallenge);

            return Forbid();
        }

        /// <summary>
        /// POST — Receber mensagens do WhatsApp.
        /// Filtros aplicados: ValidateWhatsAppSignatureFilter → WhatsAppConcurrencyGuardFilter
        /// </summary>
        [HttpPost("whatsapp")]
        [EnableRateLimiting("webhook")]
        [RequestSizeLimit(1_048_576)]
        [ServiceFilter(typeof(ValidateWhatsAppSignatureFilter), Order = -200)]
        [ServiceFilter(typeof(WhatsAppConcurrencyGuardFilter), Order = -100)]
        public async Task<IActionResult> ReceiveWhatsAppMessage()
        {
            try
            {
                JsonElement root = await ReadRequestRootAsync();

                foreach (IncomingMessage msg in WhatsAppWebhookParser.ParseMessages(root))
                {
                    if (string.IsNullOrWhiteSpace(msg.MessageId))
                        continue;

                    if (!IsAcceptedWhatsAppMessageId(msg.MessageId))
                        continue;

                    if (msg.HasLocation)
                    {
                        _logger.LogInformation(
                            "📍 WhatsApp localização recebida: From={From}, Latitude={Latitude}, Longitude={Longitude}, Name={Name}, Address={Address}",
                            msg.From,
                            msg.Latitude,
                            msg.Longitude,
                            string.IsNullOrWhiteSpace(msg.LocationName) ? "(null)" : msg.LocationName,
                            string.IsNullOrWhiteSpace(msg.LocationAddress) ? "(null)" : msg.LocationAddress);
                    }

                    _ = SafeProcessMessageAsync(msg, releaseWhatsAppSenderLock: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar webhook WhatsApp");
            }

            return Ok();
        }

        /// <summary>
        /// POST — Receber Activities do Microsoft Teams Bot Framework.
        /// Filtro aplicado: ValidateTeamsJwtFilter
        /// </summary>
        [HttpPost("teams")]
        [EnableRateLimiting("webhook")]
        [RequestSizeLimit(1_048_576)]
        [ServiceFilter(typeof(ValidateTeamsJwtFilter))]
        public async Task<IActionResult> ReceiveTeamsActivity()
        {
            try
            {
                JsonElement root = await ReadRequestRootAsync();

                var activity = JsonSerializer.Deserialize<TeamsActivity>(root.GetRawText());

                if (activity == null)
                    return Ok();

                if (TeamsWebhookParser.IsReadReceipt(activity, out string? lastReadId))
                {
                    _logger.LogInformation(
                        "👁️ Teams read receipt: FromId={FromId}, ConversationId={ConversationId}, LastReadMessageId={LastReadMessageId}",
                        activity.From?.Id,
                        activity.Conversation?.Id,
                        lastReadId ?? "(null)");

                    return Ok();
                }

                if (!TeamsWebhookParser.TryParseMessage(activity, root, out IncomingMessage msg))
                    return Ok();

                string recipientId = activity.Recipient?.Id ?? string.Empty;
                string fromId = activity.From?.Id ?? string.Empty;
                string conversationId = activity.Conversation?.Id ?? string.Empty;

                _logger.LogInformation(
                    "Teams diagnóstico: ActivityId={ActivityId}, RecipientId={RecipientId}, FromId={FromId}, ConversationId={ConversationId}, ServiceUrl={ServiceUrl}, ConfiguredBotId={ConfiguredBotId}, ConfiguredTenantId={ConfiguredTenantId}",
                    activity.Id,
                    recipientId,
                    fromId,
                    conversationId,
                    activity.ServiceUrl,
                    _teamsSettings.BotId,
                    _teamsSettings.TenantId);

                if (!string.IsNullOrWhiteSpace(recipientId) &&
                    !string.IsNullOrWhiteSpace(_teamsSettings.BotId) &&
                    !recipientId.Contains(_teamsSettings.BotId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Teams possível mismatch de bot: Recipient.Id={RecipientId} não contém BotId configurado={ConfiguredBotId}. Isto pode causar 401/403 no envio.",
                        recipientId,
                        _teamsSettings.BotId);

                    // Fast-fail: se a Activity é de outro bot, qualquer tentativa de reply
                    // tende a falhar com 401/403 (ex: failed to decrypt conversation id).
                    // Retornamos 200 para não provocar retries desnecessários do Teams.
                    return Ok();
                }

                if (string.IsNullOrWhiteSpace(msg.MessageId))
                    return Ok();

                if (MessageProcessingService.IsDuplicateMessage(msg.MessageId))
                    return Ok();

                // Se o e-mail não vier no JSON original (comum no Teams), fazemos fetch explícito
                // tal como o TeamsInfo.GetMemberAsync faria, mas via a nossa classe centralizada.
                if (string.IsNullOrWhiteSpace(msg.UserEmail) &&
                    !string.IsNullOrWhiteSpace(fromId) &&
                    !string.IsNullOrWhiteSpace(conversationId) &&
                    !string.IsNullOrWhiteSpace(activity.ServiceUrl))
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    var factory = scope.ServiceProvider.GetRequiredService<MessagingServiceFactory>();
                    if (factory.GetService(MessagePlatform.Teams) is TeamsService teamsService)
                    {
                        string? email = await teamsService.GetUserEmailAsync(activity.ServiceUrl, conversationId, fromId);
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            msg.UserEmail = email;
                            // Preencher também o UserId com o email para facilitar a correlação se desejado,
                            // embora o AadObjectId continue a ser excelente para empresas.
                        }
                    }
                }

                _ = SafeProcessMessageAsync(msg, releaseWhatsAppSenderLock: false);
                return Ok();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro ao fazer parse da Activity Teams");
                return BadRequest("JSON inválido");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar Activity Teams");
                return StatusCode(500);
            }
        }

        private async Task SafeProcessMessageAsync(IncomingMessage msg, bool releaseWhatsAppSenderLock)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<MessageProcessingService>();
                var factory = scope.ServiceProvider.GetRequiredService<MessagingServiceFactory>();
                var service = factory.GetService(msg.Platform);

                await processingService.ProcessMessageAsync(msg, service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar mensagem em background: {MessageId} via {Platform}",
                    msg.MessageId, msg.Platform);
            }
            finally
            {
                if (releaseWhatsAppSenderLock && msg.Platform == MessagePlatform.WhatsApp)
                    _concurrencyGuard.ReleaseSenderLock(msg.From);
            }
        }

        private bool IsAcceptedWhatsAppMessageId(string messageId)
        {
            if (HttpContext.Items.TryGetValue(WhatsAppConcurrencyGuardFilter.AcceptedMessageIdsItemKey, out object? value) &&
                value is HashSet<string> acceptedIds)
            {
                return acceptedIds.Contains(messageId);
            }

            return false;
        }

        private async Task<JsonElement> ReadRequestRootAsync()
        {
            using JsonDocument document = await JsonDocument.ParseAsync(Request.Body);
            return document.RootElement.Clone();
        }

    }
}
