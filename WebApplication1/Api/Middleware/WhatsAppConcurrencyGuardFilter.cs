using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebApplication1.Application;

namespace WebApplication1.Api.Middleware
{
    /// <summary>
    /// Filtro de pré-processamento para o webhook WhatsApp.
    /// Responsável por deduplicação e lock por remetente.
    /// 
    /// Comportamento:
    ///   - Valida cada mensagem (MessageId + remetente)
    ///   - Rejeita duplicadas ou de remetentes já em processamento
    ///   - Guarda MessageIds aceites para o controller processar
    ///   - Devolve 200 OK imediatamente se tudo for spam/duplicado
    /// </summary>
    public sealed class WhatsAppConcurrencyGuardFilter : IAsyncActionFilter, IOrderedFilter
    {
        public const string AcceptedMessageIdsItemKey = "WhatsAppAcceptedMessageIds";

        private readonly WebhookConcurrencyGuard _guard;
        private readonly ILogger<WhatsAppConcurrencyGuardFilter> _logger;

        public WhatsAppConcurrencyGuardFilter(
            WebhookConcurrencyGuard guard,
            ILogger<WhatsAppConcurrencyGuardFilter> logger)
        {
            _guard = guard;
            _logger = logger;
        }

        public int Order => -100;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var request = context.HttpContext.Request;

            if (!HttpMethods.IsPost(request.Method))
            {
                await next();
                return;
            }

            request.EnableBuffering();

            var acceptedMessageIds = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                using JsonDocument document = await JsonDocument.ParseAsync(request.Body, cancellationToken: context.HttpContext.RequestAborted);
                JsonElement root = document.RootElement;

                if (root.TryGetProperty("entry", out JsonElement entryArray))
                {
                    foreach (JsonElement entry in entryArray.EnumerateArray())
                    {
                        if (!entry.TryGetProperty("changes", out JsonElement changesArray))
                            continue;

                        foreach (JsonElement change in changesArray.EnumerateArray())
                        {
                            if (!change.TryGetProperty("value", out JsonElement value) ||
                                !value.TryGetProperty("messages", out JsonElement messagesArray))
                                continue;

                            foreach (JsonElement message in messagesArray.EnumerateArray())
                            {
                                string messageId = message.TryGetProperty("id", out JsonElement idEl)
                                    ? idEl.GetString() ?? string.Empty
                                    : string.Empty;

                                if (string.IsNullOrWhiteSpace(messageId))
                                    continue;

                                if (!_guard.TryRegisterMessageId(messageId))
                                {
                                    _logger.LogInformation("Mensagem WhatsApp duplicada rejeitada: {MessageId}", messageId);
                                    continue;
                                }

                                string from = message.TryGetProperty("from", out JsonElement fromEl)
                                    ? fromEl.GetString() ?? string.Empty
                                    : string.Empty;

                                if (string.IsNullOrWhiteSpace(from))
                                    continue;

                                if (!_guard.TryAcquireSenderLock(from))
                                {
                                    _logger.LogInformation("Mensagem WhatsApp rejeitada (remetente em processamento): {From}", from);
                                    continue;
                                }

                                acceptedMessageIds.Add(messageId);
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "JSON inválido no filtro WhatsApp");
            }
            finally
            {
                request.Body.Position = 0;
            }

            if (acceptedMessageIds.Count == 0)
            {
                context.Result = new OkResult();
                return;
            }

            context.HttpContext.Items[AcceptedMessageIdsItemKey] = acceptedMessageIds;
            await next();
        }
    }
}
