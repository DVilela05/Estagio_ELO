using System.Net;
using System.Text.Json;

namespace WebApplication1.Tests.Integration
{
    /// <summary>
    /// Testes de integração HTTP end-to-end para o endpoint Health.
    /// Verifica que o health check funciona e retorna os campos esperados.
    /// </summary>
    public class HealthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public HealthIntegrationTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetHealth_ReturnsOk()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetHealth_ReturnsHealthyStatus()
        {
            // Act
            var response = await _client.GetAsync("/health");
            var body = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("healthy", body, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetHealth_ReturnsJsonWithExpectedFields()
        {
            // Act
            var response = await _client.GetAsync("/health");
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Assert — verifica campos obrigatórios
            Assert.True(root.TryGetProperty("status", out var statusEl));
            Assert.Equal("healthy", statusEl.GetString());

            Assert.True(root.TryGetProperty("timestamp", out _));
            Assert.True(root.TryGetProperty("uptime", out _));
            Assert.True(root.TryGetProperty("version", out _));
        }

        [Fact]
        public async Task GetHealth_ReturnsJsonContentType()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        }
    }
}
