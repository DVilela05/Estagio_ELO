using WebApplication1.Core.Models;

namespace WebApplication1.Tests
{
    public class MessagePlatformTests
    {
        [Fact]
        public void MessagePlatform_WhatsApp_HasCorrectValue()
        {
            // Assert
            Assert.Equal(0, (int)MessagePlatform.WhatsApp);
        }

        [Fact]
        public void MessagePlatform_Teams_HasCorrectValue()
        {
            // Assert
            Assert.Equal(1, (int)MessagePlatform.Teams);
        }

        [Fact]
        public void MessagePlatform_CanBeAssignedAndRetrieved()
        {
            // Arrange
            var platform = MessagePlatform.WhatsApp;

            // Assert
            Assert.Equal(MessagePlatform.WhatsApp, platform);
        }

        [Fact]
        public void MessagePlatform_Teams_CanBeAssignedAndRetrieved()
        {
            // Arrange
            var platform = MessagePlatform.Teams;

            // Assert
            Assert.Equal(MessagePlatform.Teams, platform);
        }

        [Fact]
        public void MessagePlatform_WhatsAppAndTeams_AreDifferent()
        {
            // Assert
            Assert.NotEqual(MessagePlatform.WhatsApp, MessagePlatform.Teams);
        }
    }
}
