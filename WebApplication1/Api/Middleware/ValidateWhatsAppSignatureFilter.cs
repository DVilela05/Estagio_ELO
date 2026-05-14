using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using WebApplication1.Infrastructure.Configuration;

namespace WebApplication1.Api.Middleware
{
    /// <summary>
    /// Filtro de segurança que valida a assinatura X-Hub-Signature-256 da Meta.
    /// 
    /// A Meta assina todos os payloads de webhook com o App Secret da tua app.
    /// Isto garante que o pedido veio REALMENTE da Meta e não de um atacante.
    /// 
    /// Sem isto, qualquer pessoa que saiba o URL do teu webhook pode enviar
    /// payloads falsos e a tua app vai processá-los como se fossem reais.
    /// 
    /// Uso: colocar [ServiceFilter(typeof(ValidateWhatsAppSignatureFilter))]
    ///      no método POST do controller.
    /// </summary>
    public class ValidateWhatsAppSignatureFilter : IAsyncActionFilter
    {
        private readonly WhatsAppSettings _settings;
        private readonly ILogger<ValidateWhatsAppSignatureFilter> _logger;

        public ValidateWhatsAppSignatureFilter(
            IOptions<WhatsAppSettings> settings,
            ILogger<ValidateWhatsAppSignatureFilter> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Se o AppSecret não estiver configurado, deixamos passar (modo dev).
            // Em produção NUNCA deve estar vazio.
            if (string.IsNullOrEmpty(_settings.AppSecret) || _settings.AppSecret == "COLOCA_AQUI_O_APP_SECRET_DA_META")
            {
                _logger.LogWarning(
                    "AppSecret não configurado — validação de assinatura desativada. " +
                    "Configura o AppSecret nos User Secrets antes de ir para produção!");
                await next();
                return;
            }

            var request = context.HttpContext.Request;

            // Só validamos pedidos POST (o GET de verificação não tem assinatura).
            if (request.Method != "POST")
            {
                await next();
                return;
            }

            // 1. Obter o header X-Hub-Signature-256 enviado pela Meta.
            if (!request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader))
            {
                _logger.LogWarning("🚫 Pedido POST sem header X-Hub-Signature-256 — rejeitado.");
                context.Result = new UnauthorizedObjectResult("Missing signature header.");
                return;
            }

            string signature = signatureHeader.ToString();

            // 2. Ler o body do pedido (precisamos do raw bytes para calcular o hash).
            request.EnableBuffering(); // Permite ler o body mais do que uma vez.
            request.Body.Position = 0;
            byte[] bodyBytes = await ReadBodyBytesAsync(request.Body);
            request.Body.Position = 0; // Reset para o controller poder ler depois.

            // 3. Calcular o HMAC-SHA256 com o nosso AppSecret.
            string expectedSignature = ComputeHmacSha256(bodyBytes, _settings.AppSecret);

            // 4. Comparar as assinaturas de forma segura (timing-safe).
            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(expectedSignature)))
            {
                _logger.LogWarning("🚫 Assinatura X-Hub-Signature-256 inválida — pedido rejeitado.");
                context.Result = new UnauthorizedObjectResult("Invalid signature.");
                return;
            }

            _logger.LogDebug("✅ Assinatura X-Hub-Signature-256 válida.");
            await next();
        }

        /// <summary>
        /// Lê todos os bytes do body do pedido HTTP.
        /// </summary>
        private static async Task<byte[]> ReadBodyBytesAsync(Stream body)
        {
            using var ms = new MemoryStream();
            await body.CopyToAsync(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Calcula o HMAC-SHA256 de um payload usando o AppSecret como chave.
        /// Devolve no formato "sha256=abcdef..." (como a Meta envia).
        /// </summary>
        private static string ComputeHmacSha256(byte[] payload, string secret)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
            using var hmac = new HMACSHA256(keyBytes);
            byte[] hash = hmac.ComputeHash(payload);
            string hex = Convert.ToHexString(hash).ToLowerInvariant();
            return $"sha256={hex}";
        }
    }
}
