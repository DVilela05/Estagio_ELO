using System.Net;

namespace WebApplication1.Tests.Integration
{
    /// <summary>
    /// Testes de integração para verificar que os headers de segurança OWASP
    /// estão presentes em TODAS as respostas HTTP.
    /// 
    /// Baseado nas recomendações:
    /// - OWASP Secure Headers Project
    /// - OWASP .NET Security Cheat Sheet
    /// </summary>
    public class SecurityHeadersIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public SecurityHeadersIntegrationTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task AllResponses_ContainXFrameOptionsDeny()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert — Previne clickjacking
            Assert.True(response.Headers.Contains("X-Frame-Options"));
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
        }

        [Fact]
        public async Task AllResponses_ContainXContentTypeOptionsNosniff()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert — Previne MIME type sniffing
            Assert.True(response.Headers.Contains("X-Content-Type-Options"));
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
        }

        [Fact]
        public async Task AllResponses_ContainReferrerPolicy()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert — Controla o envio do referrer
            Assert.True(response.Headers.Contains("Referrer-Policy"));
            Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").First());
        }

        [Fact]
        public async Task AllResponses_ContainPermissionsPolicy()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert — Desativa APIs do browser não utilizadas
            Assert.True(response.Headers.Contains("Permissions-Policy"));
            var value = response.Headers.GetValues("Permissions-Policy").First();
            Assert.Contains("camera=()", value);
            Assert.Contains("microphone=()", value);
            Assert.Contains("geolocation=()", value);
        }

        [Fact]
        public async Task AllResponses_ContainContentSecurityPolicy()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert — CSP para API (bloqueia tudo)
            Assert.True(response.Headers.Contains("Content-Security-Policy"));
            var csp = response.Headers.GetValues("Content-Security-Policy").First();
            Assert.Contains("default-src 'none'", csp);
            Assert.Contains("frame-ancestors 'none'", csp);
        }

        [Fact]
        public async Task AllResponses_ContainXXssProtectionDisabled()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert — XSS Auditor desativado (recomendação OWASP moderna)
            Assert.True(response.Headers.Contains("X-XSS-Protection"));
            Assert.Equal("0", response.Headers.GetValues("X-XSS-Protection").First());
        }

        [Fact]
        public async Task AllResponses_DoNotContainServerHeader()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert — Header Server não deve revelar info do servidor
            // Nota: TestServer pode não ter Server header de qualquer forma,
            // mas verificamos que não aparece Kestrel nem IIS.
            if (response.Headers.Contains("Server"))
            {
                var server = response.Headers.GetValues("Server").First();
                Assert.DoesNotContain("Kestrel", server);
                Assert.DoesNotContain("IIS", server);
            }
        }

        [Fact]
        public async Task AllResponses_DoNotContainXPoweredByHeader()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert — Não revelar tecnologia usada
            Assert.False(response.Headers.Contains("X-Powered-By"));
        }

        [Fact]
        public async Task SecurityHeaders_PresentOnWebhookEndpoints()
        {
            // Verifica que os headers também estão nos endpoints de webhook (não só no health)
            // Act
            var response = await _client.GetAsync("/api/webhook/whatsapp?hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=123");

            // Assert — Mesmo em respostas de erro, os headers devem estar presentes
            Assert.True(response.Headers.Contains("X-Frame-Options"));
            Assert.True(response.Headers.Contains("X-Content-Type-Options"));
            Assert.True(response.Headers.Contains("Referrer-Policy"));
        }

        [Fact]
        public async Task AllResponses_ContainCorrelationId()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert — Cada pedido deve ter um Correlation ID para rastreamento
            Assert.True(response.Headers.Contains("X-Correlation-ID"));
            var correlationId = response.Headers.GetValues("X-Correlation-ID").First();
            Assert.True(Guid.TryParse(correlationId, out _), "Correlation ID deve ser um GUID válido");
        }
    }
}
