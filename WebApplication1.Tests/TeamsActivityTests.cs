using WebApplication1.Infrastructure.Messaging;

namespace WebApplication1.Tests
{
    /// <summary>
    /// Testes para os DTOs do Teams (TeamsActivity, TeamsChannelAccount, etc.).
    /// Garante que a deserialização e os modelos funcionam corretamente.
    /// </summary>
    public class TeamsActivityTests
    {
        [Fact]
        public void TeamsActivity_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var activity = new TeamsActivity();

            // Assert
            Assert.Equal(string.Empty, activity.Type);
            Assert.Equal(string.Empty, activity.Id);
            Assert.Equal(string.Empty, activity.ServiceUrl);
            Assert.Equal(string.Empty, activity.ChannelId);
            Assert.Equal(string.Empty, activity.Text);
            Assert.Null(activity.Name);
            Assert.False(activity.Value.HasValue);
            Assert.NotNull(activity.From);
            Assert.NotNull(activity.Conversation);
            Assert.NotNull(activity.Recipient);
            Assert.Null(activity.ChannelData);
            Assert.Null(activity.ReplyToId);
        }

        [Fact]
        public void TeamsActivity_CanSetAllProperties()
        {
            // Arrange & Act
            var activity = new TeamsActivity
            {
                Type = "message",
                Id = "activity-123",
                Timestamp = new DateTime(2026, 2, 19, 10, 30, 0),
                ServiceUrl = "https://smba.trafficmanager.net/emea/",
                ChannelId = "msteams",
                Text = "presente",
                From = new TeamsChannelAccount { Id = "user-1", Name = "João" },
                Conversation = new TeamsConversationAccount { Id = "conv-1", ConversationType = "personal" },
                Recipient = new TeamsChannelAccount { Id = "bot-1", Name = "ELO Bot" },
                ReplyToId = "reply-to-456"
            };

            // Assert
            Assert.Equal("message", activity.Type);
            Assert.Equal("activity-123", activity.Id);
            Assert.Equal("https://smba.trafficmanager.net/emea/", activity.ServiceUrl);
            Assert.Equal("msteams", activity.ChannelId);
            Assert.Equal("presente", activity.Text);
            Assert.Equal("user-1", activity.From.Id);
            Assert.Equal("João", activity.From.Name);
            Assert.Equal("conv-1", activity.Conversation.Id);
            Assert.Equal("personal", activity.Conversation.ConversationType);
            Assert.Equal("bot-1", activity.Recipient.Id);
            Assert.Equal("reply-to-456", activity.ReplyToId);
        }

        [Fact]
        public void TeamsChannelAccount_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var account = new TeamsChannelAccount();

            // Assert
            Assert.Equal(string.Empty, account.Id);
            Assert.Equal(string.Empty, account.Name);
            Assert.Null(account.AadObjectId);
        }

        [Fact]
        public void TeamsConversationAccount_CanSetTenantId()
        {
            // Arrange & Act
            var conversation = new TeamsConversationAccount
            {
                Id = "conv-123",
                ConversationType = "personal",
                TenantId = "tenant-abc"
            };

            // Assert
            Assert.Equal("conv-123", conversation.Id);
            Assert.Equal("personal", conversation.ConversationType);
            Assert.Equal("tenant-abc", conversation.TenantId);
        }

        [Fact]
        public void TeamsChannelData_WithTenant_HasCorrectValues()
        {
            // Arrange & Act
            var channelData = new TeamsChannelData
            {
                Tenant = new TeamsTenantInfo { Id = "tenant-123" },
                EventType = "channelCreated"
            };

            // Assert
            Assert.NotNull(channelData.Tenant);
            Assert.Equal("tenant-123", channelData.Tenant.Id);
            Assert.Equal("channelCreated", channelData.EventType);
        }

        [Fact]
        public void TeamsReplyPayload_DefaultType_IsMessage()
        {
            // Arrange & Act
            var payload = new TeamsReplyPayload();

            // Assert
            Assert.Equal("message", payload.Type);
            Assert.Equal(string.Empty, payload.Text);
        }

        [Fact]
        public void TeamsReplyPayload_CanSetText()
        {
            // Arrange & Act
            var payload = new TeamsReplyPayload
            {
                Text = "✅ mensagem de *presença* recebida com sucesso!"
            };

            // Assert
            Assert.Equal("✅ mensagem de *presença* recebida com sucesso!", payload.Text);
            Assert.Equal("message", payload.Type); // tipo mantém default
        }

        [Fact]
        public void TeamsActivity_Deserialization_WorksCorrectly()
        {
            // Arrange — simula JSON do Teams
            var json = """
            {
                "type": "message",
                "id": "1234567890",
                "timestamp": "2026-02-19T10:30:00Z",
                "serviceUrl": "https://smba.trafficmanager.net/emea/",
                "channelId": "msteams",
                "from": { "id": "29:user-id", "name": "Diogo" },
                "conversation": { "id": "a:conv-id", "conversationType": "personal" },
                "recipient": { "id": "28:bot-id", "name": "ELO Bot" },
                "text": "presente"
            }
            """;

            // Act
            var activity = System.Text.Json.JsonSerializer.Deserialize<TeamsActivity>(json);

            // Assert
            Assert.NotNull(activity);
            Assert.Equal("message", activity!.Type);
            Assert.Equal("1234567890", activity.Id);
            Assert.Equal("https://smba.trafficmanager.net/emea/", activity.ServiceUrl);
            Assert.Equal("msteams", activity.ChannelId);
            Assert.Equal("presente", activity.Text);
            Assert.Equal("29:user-id", activity.From.Id);
            Assert.Equal("Diogo", activity.From.Name);
            Assert.Equal("a:conv-id", activity.Conversation.Id);
        }

        [Fact]
        public void TeamsActivity_Deserialization_ReadReceiptEvent_WorksCorrectly()
        {
            // Arrange
            var json = """
            {
                "type": "event",
                "name": "application/vnd.microsoft.readReceipt",
                "id": "evt-123",
                "serviceUrl": "https://smba.trafficmanager.net/emea/",
                "channelId": "msteams",
                "from": { "id": "29:user-id", "name": "Diogo" },
                "conversation": { "id": "a:conv-id", "conversationType": "personal" },
                "recipient": { "id": "28:bot-id", "name": "ELO Bot" },
                "value": { "lastReadMessageId": "1773851570602" }
            }
            """;

            // Act
            var activity = System.Text.Json.JsonSerializer.Deserialize<TeamsActivity>(json);

            // Assert
            Assert.NotNull(activity);
            Assert.Equal("event", activity!.Type);
            Assert.Equal("application/vnd.microsoft.readReceipt", activity.Name);
            Assert.True(activity.Value.HasValue);
            Assert.True(activity.Value!.Value.TryGetProperty("lastReadMessageId", out var el));
            Assert.Equal("1773851570602", el.GetString());
        }
    }
}
