using System.Net;
using System.Text;
using System.Text.Json;
using Moq;
using WebApplication1.Core.Interfaces;

namespace WebApplication1.Tests.Integration
{
    /// <summary>
    /// Testes de integração HTTP end-to-end para os endpoints Teams.
    /// Usa WebApplicationFactory para subir a app in-memory com mocks.
    /// 
    /// Nota: Em Development mode, o ValidateTeamsJwtFilter faz bypass,
    /// por isso não precisamos de JWT válido nos testes.
    /// </summary>
    [Collection("NonParallelIntegration")]
    public class TeamsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory _factory;

        public TeamsIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private void ResetTeamsMockInvocations()
            => _factory.MockTeamsService.Invocations.Clear();

        private async Task WaitForTeamsSendInvocationAsync(int timeoutMs = 6000)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                if (_factory.MockTeamsService.Invocations.Any(i => i.Method.Name == nameof(IMessagingService.SendTextMessageAsync)))
                    return;

                await Task.Delay(50);
            }
        }

        // =====================================================================
        // POST /api/webhook/teams — Mensagens válidas
        // =====================================================================

        [Fact]
        public async Task PostTeams_WithHelpMessageActivity_ReturnsOk_AndSendsReplyToActivityEndpoint()
        {
            // Arrange
            ResetTeamsMockInvocations();
            var payload = CreateTeamsActivity("act-001", "message", "ajuda");
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/teams", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await WaitForTeamsSendInvocationAsync();

            _factory.MockTeamsService.Verify(
                s => s.SendTextMessageAsync(
                    It.Is<string>(to => to.Contains("/v3/conversations/conv-test-001/activities/act-001")),
                    It.IsAny<string>(),
                    It.Is<string?>(replyTo => replyTo == "act-001")),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task PostTeams_WithReadReceiptEvent_ReturnsOk_AndDoesNotSendReply()
        {
            // Arrange
            ResetTeamsMockInvocations();
            var payload = CreateTeamsReadReceiptEvent("rr-001", "1692206589131");
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/teams", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            _factory.MockTeamsService.Verify(
                s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
                Times.Never);
        }

        [Fact]
        public async Task PostTeams_WithHelpCommand_ReturnsOk()
        {
            // Arrange
            var payload = CreateTeamsActivity("act-help001", "message", "ajuda");
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/teams", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostTeams_WithBotMention_RemovesMentionAndProcesses()
        {
            // Arrange — Teams adiciona <at>BotName</at> antes do texto
            var payload = CreateTeamsActivity("act-mention001", "message", "<at>MyBot</at> presente");
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/teams", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // =====================================================================
        // POST /api/webhook/teams — Activities não-mensagem
        // =====================================================================

        [Fact]
        public async Task PostTeams_WithConversationUpdateActivity_ReturnsOk()
        {
            // Arrange — conversationUpdate não é processada como mensagem
            var payload = CreateTeamsActivity("act-update001", "conversationUpdate", "");
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/teams", content);

            // Assert — Ignora e retorna 200
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostTeams_WithTypingActivity_ReturnsOk()
        {
            // Arrange
            var payload = CreateTeamsActivity("act-typing001", "typing", "");
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/teams", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostTeams_WithEmptyText_ReturnsOk()
        {
            // Arrange — mensagem sem texto
            var payload = CreateTeamsActivity("act-empty001", "message", "");
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/teams", content);

            // Assert — Mensagem sem texto é ignorada
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // =====================================================================
        // POST /api/webhook/teams — Erros e edge cases
        // =====================================================================

        [Fact]
        public async Task PostTeams_WithInvalidJson_ReturnsBadRequest()
        {
            // Arrange
            var content = new StringContent("not json at all {{{", Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/teams", content);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task PostTeams_WithNullActivity_ReturnsOk()
        {
            // Arrange — JSON válido mas que deserializa para null
            var content = new StringContent("null", Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/teams", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostTeams_WithMessageWithoutId_ReturnsOk()
        {
            // Arrange — Activity sem ID
            var activity = new
            {
                type = "message",
                id = "",
                timestamp = DateTime.UtcNow,
                serviceUrl = "https://smba.trafficmanager.net/emea/",
                channelId = "msteams",
                from = new { id = "29:user-001", name = "Diogo", aadObjectId = "aad-obj-001" },
                conversation = new { id = "conv-001", tenantId = "common" },
                recipient = new { id = "28:bot-001", name = "TestBot" },
                text = "presente"
            };
            var content = new StringContent(JsonSerializer.Serialize(activity), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/teams", content);

            // Assert — Sem ID, é ignorada e retorna 200
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostTeams_WithUserIdentification_ReturnsOk()
        {
            // Arrange — Activity com informação completa do utilizador
            var activity = new
            {
                type = "message",
                id = "act-user001",
                timestamp = DateTime.UtcNow,
                serviceUrl = "https://smba.trafficmanager.net/emea/",
                channelId = "msteams",
                from = new
                {
                    id = "29:diogo-user-id",
                    name = "Diogo Pereira",
                    aadObjectId = "aad-12345-abcde"
                },
                conversation = new { id = "conv-diogo-001", tenantId = "tenant-id-001" },
                recipient = new { id = "28:bot-001", name = "ELOBot" },
                text = "presente"
            };
            var content = new StringContent(JsonSerializer.Serialize(activity), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/webhook/teams", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// Cria um payload JSON de Activity do Teams Bot Framework.
        /// </summary>
        private static string CreateTeamsActivity(string id, string type, string text)
        {
            var activity = new
            {
                type = type,
                id = id,
                timestamp = DateTime.UtcNow,
                serviceUrl = "https://smba.trafficmanager.net/emea/",
                channelId = "msteams",
                from = new
                {
                    id = $"29:test-user-{id}",
                    name = "TestUser",
                    aadObjectId = $"aad-test-{id}"
                },
                conversation = new
                {
                    id = "conv-test-001",
                    tenantId = "common"
                },
                recipient = new
                {
                    id = "",
                    name = "TestBot"
                },
                text = text
            };

            return JsonSerializer.Serialize(activity);
        }

        private static string CreateTeamsReadReceiptEvent(string id, string lastReadMessageId)
        {
            var activity = new
            {
                type = "event",
                name = "application/vnd.microsoft.readReceipt",
                id = id,
                timestamp = DateTime.UtcNow,
                serviceUrl = "https://smba.trafficmanager.net/emea/",
                channelId = "msteams",
                from = new
                {
                    id = $"29:test-user-{id}",
                    name = "TestUser",
                    aadObjectId = $"aad-test-{id}"
                },
                conversation = new
                {
                    id = "conv-test-001",
                    tenantId = "common"
                },
                recipient = new
                {
                    id = "",
                    name = "TestBot"
                },
                value = new
                {
                    lastReadMessageId = lastReadMessageId
                }
            };

            return JsonSerializer.Serialize(activity);
        }
    }
}
