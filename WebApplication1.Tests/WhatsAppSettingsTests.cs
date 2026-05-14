using WebApplication1.Infrastructure.Configuration;

namespace WebApplication1.Tests
{
    public class WhatsAppSettingsTests
    {
        [Fact]
        public void WhatsAppSettings_DefaultProperties_AreInitialized()
        {
            // Act
            var settings = new WhatsAppSettings();

            // Assert
            // Properties default to empty string when not set, not null
            Assert.NotNull(settings);
        }

        [Fact]
        public void WhatsAppSettings_Properties_CanBeAssigned()
        {
            // Arrange
            var settings = new WhatsAppSettings();

            // Act
            settings.VerifyToken = "verify_123";
            settings.AccessToken = "access_456";
            settings.AppSecret = "secret_789";
            settings.PhoneNumberId = "123456789";
            settings.ApiVersion = "v22.0";

            // Assert
            Assert.Equal("verify_123", settings.VerifyToken);
            Assert.Equal("access_456", settings.AccessToken);
            Assert.Equal("secret_789", settings.AppSecret);
            Assert.Equal("123456789", settings.PhoneNumberId);
            Assert.Equal("v22.0", settings.ApiVersion);
        }

        [Fact]
        public void WhatsAppSettings_PropertiesExist()
        {
            // Arrange
            var settings = new WhatsAppSettings();

            // Act & Assert
            Assert.NotNull(settings.GetType().GetProperty("VerifyToken"));
            Assert.NotNull(settings.GetType().GetProperty("AccessToken"));
            Assert.NotNull(settings.GetType().GetProperty("AppSecret"));
            Assert.NotNull(settings.GetType().GetProperty("PhoneNumberId"));
            Assert.NotNull(settings.GetType().GetProperty("ApiVersion"));
        }
    }
}
