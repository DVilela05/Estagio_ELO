using WebApplication1.Infrastructure.Configuration;

namespace WebApplication1.Tests
{
    /// <summary>
    /// Testes para o modelo TeamsSettings.
    /// Espelho dos WhatsAppSettingsTests para manter consistência.
    /// </summary>
    public class TeamsSettingsTests
    {
        [Fact]
        public void TeamsSettings_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var settings = new TeamsSettings();

            // Assert
            Assert.Equal(string.Empty, settings.BotId);
            Assert.Equal(string.Empty, settings.ClientSecret);
            Assert.Equal("botframework.com", settings.TenantId);
            Assert.Equal("https://login.microsoftonline.com", settings.LoginUrl);
            Assert.Equal("https://api.botframework.com/.default", settings.Scope);
        }

        [Fact]
        public void TeamsSettings_CanSetAllProperties()
        {
            // Arrange
            var settings = new TeamsSettings
            {
                BotId = "0c4d4876-e7d0-4a03-aa96-572ad1abc4e1",
                ClientSecret = "test_secret",
                TenantId = "my-tenant-id",
                LoginUrl = "https://custom-login.example.com",
                Scope = "https://custom-scope/.default"
            };

            // Assert
            Assert.Equal("0c4d4876-e7d0-4a03-aa96-572ad1abc4e1", settings.BotId);
            Assert.Equal("test_secret", settings.ClientSecret);
            Assert.Equal("my-tenant-id", settings.TenantId);
            Assert.Equal("https://custom-login.example.com", settings.LoginUrl);
            Assert.Equal("https://custom-scope/.default", settings.Scope);
        }

        [Fact]
        public void TeamsSettings_BotId_IsSensitiveField()
        {
            // Arrange — BotId deve poder ser configurado
            var settings = new TeamsSettings();

            // Act
            settings.BotId = "0c4d4876-e7d0-4a03-aa96-572ad1abc4e1";

            // Assert
            Assert.NotEmpty(settings.BotId);
            Assert.Contains("-", settings.BotId); // UUID format
        }
    }
}
