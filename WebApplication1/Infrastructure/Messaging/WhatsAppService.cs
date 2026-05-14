using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.Configuration;
using WebApplication1.Infrastructure.Logging;

namespace WebApplication1.Infrastructure.Messaging
{
    /// <summary>
    /// Implementação do IMessagingService para o WhatsApp Business API.
    /// 
    /// Toda a lógica ESPECÍFICA do WhatsApp fica aqui.
    /// Para adicionar outra plataforma, cria outro serviço que implementa
    /// a mesma interface IMessagingService mas com a lógica dessa plataforma.
    /// </summary>
    public class WhatsAppService : IMessagingService
    {
        private readonly HttpClient _httpClient;
        private readonly WhatsAppSettings _settings;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(
            HttpClient httpClient,
            IOptions<WhatsAppSettings> settings,
            ILogger<WhatsAppService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public MessagePlatform Platform => MessagePlatform.WhatsApp;

        /// <inheritdoc />
        public async Task<bool> MarkAsReadAsync(string messageId)
        {
            try
            {
                var payload = new
                {
                    messaging_product = "whatsapp",
                    status = "read",
                    message_id = messageId
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_settings.PhoneNumberId}/messages", content);

                if (response.IsSuccessStatusCode)
                    _logger.LogDebug("Mensagem {MessageId} marcada como lida", messageId);
                else
                    _logger.LogWarning("Falha ao marcar mensagem {MessageId} como lida: {StatusCode}",
                        messageId, response.StatusCode);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao marcar mensagem como lida: {MessageId}", messageId);
                ConsoleLogger.Error($"Erro ao marcar como lida: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SendTextMessageAsync(string to, string text, string? replyToMessageId = null)
        {
            try
            {
                object payload;

                if (!string.IsNullOrEmpty(replyToMessageId))
                {
                    // Resposta vinculada (citação) — mostra a mensagem original no chat.
                    // Usado quando há spam: o utilizador vê claramente qual mensagem o bot respondeu.
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        to = to,
                        context = new { message_id = replyToMessageId },
                        type = "text",
                        text = new { body = text }
                    };
                    _logger.LogDebug("Resposta vinculada à mensagem {ReplyToId} para {To}", replyToMessageId, to);
                }
                else
                {
                    // Resposta normal (sem citação) — mensagem única, sem spam.
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        to = to,
                        text = new { body = text }
                    };
                }

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_settings.PhoneNumberId}/messages", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Mensagem enviada para {To}", to);
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();

                    // Rate limit da Meta: demasiadas mensagens para o mesmo número em pouco tempo
                    if (errorBody.Contains("131056") || errorBody.Contains("rate limit"))
                    {
                        _logger.LogWarning(
                            "⚠️ Rate limit da Meta para {To} — demasiadas mensagens num curto período. A mensagem NÃO foi entregue.",
                            to);
                        ConsoleLogger.Error(
                            $"Rate limit da Meta para {to} — a mensagem não foi entregue. Aguardar antes de reenviar.");
                    }
                    else
                    {
                        _logger.LogError("Erro da API Meta ao enviar para {To}: {Error}", to, errorBody);
                        ConsoleLogger.Error($"Erro da API Meta: {errorBody}");
                    }
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar resposta para {To}", to);
                ConsoleLogger.Error($"Erro ao enviar resposta: {ex.Message}");
                return false;
            }
        }
    }
}
