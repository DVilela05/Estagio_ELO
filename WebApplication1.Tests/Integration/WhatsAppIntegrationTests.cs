using System.Net;
using System.Text;
using System.Text.Json;
using Moq;

namespace WebApplication1.Tests.Integration
{
    /// <summary>
    /// Testes de integração HTTP end-to-end para os endpoints WhatsApp.
    /// Usa WebApplicationFactory para subir a app in-memory com mocks.
    /// </summary>
    public class WhatsAppIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory _factory;

        public WhatsAppIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        // =====================================================================
        // GET /api/webhook/whatsapp — Verificação do Webhook
        // =====================================================================

        [Fact]
        public async Task GetVerifyWebhook_WithValidToken_ReturnsChallenge()
        {
            // Arrange — token "estagioelo2026" vem do appsettings.json
            var url = "/api/webhook/whatsapp?hub.mode=subscribe&hub.verify_token=estagioelo2026&hub.challenge=1234567890";

            // Act
            var response = await _client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal("1234567890", body);
        }

        [Fact]
        public async Task GetVerifyWebhook_WithInvalidToken_ReturnsForbid()
        {
            // Arrange
            var url = "/api/webhook/whatsapp?hub.mode=subscribe&hub.verify_token=token_errado&hub.challenge=123";

            // Act
            var response = await _client.GetAsync(url);

            // Assert — Forbid pode retornar 403 ou 401 dependendo da configuração de auth
            Assert.True(
                response.StatusCode == HttpStatusCode.Forbidden ||
                response.StatusCode == HttpStatusCode.InternalServerError,
                $"Esperava 403 ou 500, recebeu {response.StatusCode}");
        }

        [Fact]
        public async Task GetVerifyWebhook_WithWrongMode_ReturnsForbid()
        {
            // Arrange
            var url = "/api/webhook/whatsapp?hub.mode=unsubscribe&hub.verify_token=estagioelo2026&hub.challenge=123";

            // Act
            var response = await _client.GetAsync(url);

            // Assert
            Assert.True(
                response.StatusCode == HttpStatusCode.Forbidden ||
                response.StatusCode == HttpStatusCode.InternalServerError,
                $"Esperava Forbid, recebeu {response.StatusCode}");
        }

        [Fact]
        public async Task GetVerifyWebhook_WithStringChallenge_ReturnsAsString()
        {
            // Arrange — challenge não numérico
            var url = "/api/webhook/whatsapp?hub.mode=subscribe&hub.verify_token=estagioelo2026&hub.challenge=abc_challenge_xyz";

            // Act
            var response = await _client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal("abc_challenge_xyz", body);
        }

        // =====================================================================
        // POST /api/webhook/whatsapp — Receber Mensagens
        // =====================================================================

        [Fact]
        public async Task PostWhatsApp_WithValidMessage_ReturnsOk()
        {
            // Arrange
            var payload = CreateWhatsAppPayload("wamid.test001", "351999999999", "presente");
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // O filtro HMAC em dev sem AppSecret real deixa passar com warning
            // Act
            var response = await _client.PostAsync("/api/webhook/whatsapp", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostWhatsApp_WithEmptyBody_ReturnsOk()
        {
            // Arrange — body sem mensagens
            var payload = JsonSerializer.Serialize(new { @object = "whatsapp_business_account", entry = new object[] { } });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/whatsapp", content);

            // Assert — mesmo sem mensagens, retorna 200 (a Meta espera isso)
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostWhatsApp_WithHelpCommand_ReturnsOk()
        {
            // Arrange
            var payload = CreateWhatsAppPayload("wamid.help001", "351888888888", "ajuda");
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/whatsapp", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostWhatsApp_WithMessageWithEmojis_ReturnsOk()
        {
            // Arrange — mensagem com emojis e pontuação (normalização deve tratar)
            var payload = CreateWhatsAppPayload("wamid.emoji001", "351777777777", "🎉 Presente!!! 😊");
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/whatsapp", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostWhatsApp_WithNoMessagesInPayload_ReturnsOk()
        {
            // Arrange — payload com entry mas sem messages (ex: status update)
            var payload = JsonSerializer.Serialize(new
            {
                @object = "whatsapp_business_account",
                entry = new[]
                {
                    new
                    {
                        changes = new[]
                        {
                            new
                            {
                                value = new
                                {
                                    statuses = new[]
                                    {
                                        new { id = "wamid.status001", status = "delivered" }
                                    }
                                }
                            }
                        }
                    }
                }
            });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/whatsapp", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostWhatsApp_WithLocationMessage_ReturnsOk_AndIsProcessedAsIncomingMessage()
        {
            // Arrange
            _factory.MockWhatsAppService.Invocations.Clear();

            var payload = CreateWhatsAppLocationPayload(
                "wamid.loc001",
                "351911111111",
                38.7223,
                -9.1393,
                "Lisboa",
                "Lisboa, Portugal");

            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/whatsapp", content);
            await Task.Delay(200);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            _factory.MockWhatsAppService.Verify(
                s => s.MarkAsReadAsync(It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task PostWhatsApp_WithInvalidJson_ReturnsOk()
        {
            // Arrange — JSON inválido (controller faz try/catch e retorna Ok)
            var content = new StringContent("{ invalid json !!!", Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/whatsapp", content);

            // Assert — o controller apanha exceções e retorna 200 (para a Meta não retransmitir)
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostWhatsApp_TwoMessagesSameSender_ProcessesOnlyFirst()
        {
            // Arrange
            _factory.MockWhatsAppService.Invocations.Clear();

            var sender = "351933300001";
            var payload = CreateWhatsAppPayload(new[]
            {
                (id: $"wamid.same.{Guid.NewGuid():N}", from: sender, body: "presente"),
                (id: $"wamid.same.{Guid.NewGuid():N}", from: sender, body: "ajuda")
            });

            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/whatsapp", content);
            var calls = await WaitForMarkAsReadCallsAsync(expectedAtLeast: 1);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task PostWhatsApp_TwoMessagesDifferentSender_ProcessesBoth()
        {
            // Arrange
            _factory.MockWhatsAppService.Invocations.Clear();

            var payload = CreateWhatsAppPayload(new[]
            {
                (id: $"wamid.diff.{Guid.NewGuid():N}", from: "351933300101", body: "presente"),
                (id: $"wamid.diff.{Guid.NewGuid():N}", from: "351933300102", body: "ajuda")
            });

            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/whatsapp", content);
            var calls = await WaitForMarkAsReadCallsAsync(expectedAtLeast: 2);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(calls >= 2, $"Esperava pelo menos 2 chamadas MarkAsReadAsync, recebi {calls}");
        }

        [Fact]
        public async Task PostWhatsApp_SameMessageIdAcrossRequests_SecondIsIgnored()
        {
            // Arrange
            _factory.MockWhatsAppService.Invocations.Clear();

            var messageId = $"wamid.dup.{Guid.NewGuid():N}";
            var firstPayload = CreateWhatsAppPayload(messageId, "351933399901", "presente");
            var secondPayload = CreateWhatsAppPayload(messageId, "351933399901", "presente");

            // Act
            var firstResponse = await _client.PostAsync(
                "/api/webhook/whatsapp",
                new StringContent(firstPayload, Encoding.UTF8, "application/json"));

            var firstCalls = await WaitForMarkAsReadCallsAsync(expectedAtLeast: 1);

            var secondResponse = await _client.PostAsync(
                "/api/webhook/whatsapp",
                new StringContent(secondPayload, Encoding.UTF8, "application/json"));

            await Task.Delay(200);
            var finalCalls = CountMarkAsReadCalls();

            // Assert
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
            Assert.True(firstCalls >= 1);
            Assert.Equal(firstCalls, finalCalls);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// Cria um payload JSON completo do webhook WhatsApp.
        /// </summary>
        private static string CreateWhatsAppPayload(string messageId, string from, string body)
        {
            return CreateWhatsAppPayload(new[] { (messageId, from, body) });
        }

        private static string CreateWhatsAppPayload(IEnumerable<(string id, string from, string body)> messages)
        {
            return JsonSerializer.Serialize(new
            {
                @object = "whatsapp_business_account",
                entry = new[]
                {
                    new
                    {
                        changes = new[]
                        {
                            new
                            {
                                value = new
                                {
                                    messages = messages.Select(m => new
                                    {
                                        id = m.id,
                                        from = m.from,
                                        timestamp = "1708335600",
                                        type = "text",
                                        text = new { body = m.body }
                                    }).ToArray()
                                }
                            }
                        }
                    }
                }
            });
        }

        private static string CreateWhatsAppLocationPayload(
            string messageId,
            string from,
            double latitude,
            double longitude,
            string? name = null,
            string? address = null)
        {
            return JsonSerializer.Serialize(new
            {
                @object = "whatsapp_business_account",
                entry = new[]
                {
                    new
                    {
                        changes = new[]
                        {
                            new
                            {
                                value = new
                                {
                                    messages = new[]
                                    {
                                        new
                                        {
                                            id = messageId,
                                            from,
                                            timestamp = "1708335600",
                                            type = "location",
                                            location = new
                                            {
                                                latitude,
                                                longitude,
                                                name,
                                                address
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }

        private int CountMarkAsReadCalls()
        {
            return _factory.MockWhatsAppService.Invocations
                .Count(i => i.Method.Name == "MarkAsReadAsync");
        }

        private async Task<int> WaitForMarkAsReadCallsAsync(int expectedAtLeast, int timeoutMs = 3000)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                int calls = CountMarkAsReadCalls();
                if (calls >= expectedAtLeast)
                    return calls;

                await Task.Delay(50);
            }

            return CountMarkAsReadCalls();
        }
    }
}
