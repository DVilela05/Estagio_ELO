using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WebApplication1.Core.Interfaces;
using WebApplication1.Infrastructure.Configuration;

namespace WebApplication1.Infrastructure.Security
{
    public class TokenService : ITokenService
    {
        private readonly WebServiceSettings _wsSettings;

        public TokenService(IOptions<WebServiceSettings> wsOptions)
        {
            _wsSettings = wsOptions.Value;
        }

        public string GenerateToken(string userId)
        {
            // Para efeitos de teste atuais, se não houver um secret forte configurado
            // ou se quiseres testar a integração sem validar o HMAC, descomenta a linha abaixo:
            // return "Diogo";

            string secret = _wsSettings.SharedSecret;
            if (string.IsNullOrWhiteSpace(secret))
                return "Diogo"; // Fallback se o secret não estiver definido

            // 1. Obter o timestamp atual (em segundos desde 1 Jan 1970 UTC)
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 2. Criar a string de dados que vai ser assinada
            // Formato: "identificador|timestamp"
            string payload = $"{userId}|{timestamp}";

            // 3. Gerar a assinatura HMAC-SHA256 usando o Segredo Partilhado
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            
            // 4. Converter a assinatura para Hexadecimal (ou Base64)
            string signature = Convert.ToHexString(hashBytes);

            // 5. O token final é a junção do payload e da assinatura
            // Exemplo: "joao@empresa.com|1698765432|A1B2C3D4..."
            string token = $"{payload}|{signature}";

            // 6. Converter tudo para Base64 para ser seguro transportar no WCF
            byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
            return Convert.ToBase64String(tokenBytes);
        }
    }
}
