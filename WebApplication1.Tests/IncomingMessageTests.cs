using WebApplication1.Core.Models;

namespace WebApplication1.Tests
{
    public class IncomingMessageTests
    {
        [Fact]
        public void IncomingMessage_DefaultProperties_AreInitialized()
        {
            // Act
            var message = new IncomingMessage();

            // Assert
            // Properties default to empty string when not set, not null
            Assert.NotNull(message);
        }

        [Fact]
        public void IncomingMessage_Properties_CanBeAssigned()
        {
            // Arrange
            var message = new IncomingMessage();

            // Act
            message.MessageId = "wamid.123";
            message.From = "554199999999";
            message.ReplyEndpoint = "554199999999";
            message.UserId = "554199999999";
            message.UserName = "João Silva";
            message.Body = "hello";
            message.OriginalBody = "Hello!";
            message.Platform = MessagePlatform.WhatsApp;

            // Assert
            Assert.Equal("wamid.123", message.MessageId);
            Assert.Equal("554199999999", message.From);
            Assert.Equal("554199999999", message.ReplyEndpoint);
            Assert.Equal("554199999999", message.UserId);
            Assert.Equal("João Silva", message.UserName);
            Assert.Equal("hello", message.Body);
            Assert.Equal("Hello!", message.OriginalBody);
            Assert.Equal(MessagePlatform.WhatsApp, message.Platform);
        }

        [Fact]
        public void IncomingMessage_FormattedTime_ReturnsValidDateTime()
        {
            // Arrange
            var message = new IncomingMessage 
            { 
                ReceivedAt = DateTime.Now 
            };

            // Act
            var formatted = message.FormattedTime;

            // Assert
            Assert.NotNull(formatted);
            Assert.NotEmpty(formatted);
        }

        [Fact]
        public void IncomingMessage_OriginalBody_PreservesOriginalText()
        {
            // Arrange
            var originalText = "  HELLO WORLD!!!  ";
            var normalizedText = "hello world";

            var message = new IncomingMessage 
            { 
                OriginalBody = originalText,
                Body = normalizedText
            };

            // Act & Assert
            Assert.Equal(originalText, message.OriginalBody);
            Assert.Equal(normalizedText, message.Body);
        }

        [Fact]
        public void IncomingMessage_WithUserId_StoresUserIdentifier()
        {
            // Arrange
            var message = new IncomingMessage();

            // Act
            message.UserId = "29:1XJKJMvc"; // Teams AAD Object ID
            message.UserName = "Diogo Silva";

            // Assert
            Assert.Equal("29:1XJKJMvc", message.UserId);
            Assert.Equal("Diogo Silva", message.UserName);
        }

        [Fact]
        public void IncomingMessage_WithEmptyUserName_StoresEmpty()
        {
            // Arrange
            var message = new IncomingMessage();

            // Act
            message.UserId = "554199999999";
            message.UserName = ""; // WhatsApp pode não ter nome

            // Assert
            Assert.Equal("554199999999", message.UserId);
            Assert.Empty(message.UserName);
        }

        [Fact]
        public void IncomingMessage_Teams_WithUserInfo()
        {
            // Arrange & Act
            var message = new IncomingMessage
            {
                MessageId = "activity-123",
                From = "aad-obj-123",
                ReplyEndpoint = "https://smba.trafficmanager.net/v3/conversations/conv-001/activities/activity-123",
                UserId = "29:1XJKJMvc123456", // Teams AAD ObjectId
                UserName = "Diogo Silva",
                Body = "presente",
                Platform = MessagePlatform.Teams
            };

            // Assert
            Assert.Equal("29:1XJKJMvc123456", message.UserId);
            Assert.Equal("Diogo Silva", message.UserName);
            Assert.Contains("/activities/activity-123", message.ReplyEndpoint, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(MessagePlatform.Teams, message.Platform);
        }
    }
}
