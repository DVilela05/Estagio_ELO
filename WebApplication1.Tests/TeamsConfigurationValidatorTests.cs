using WebApplication1.Infrastructure.Configuration;
using WebApplication1.Core.Exceptions;

namespace WebApplication1.Tests
{
    /// <summary>
    /// Testes para ConfigurationValidator.ValidateTeamsSettings.
    /// Espelho dos testes de WhatsApp para manter consistência.
    /// </summary>
    public class TeamsConfigurationValidatorTests
    {
        [Fact]
        public void ValidateTeamsSettings_WithValidSettings_DoesNotThrow()
        {
            // Arrange
            var settings = new TeamsSettings
            {
                BotId = "0c4d4876-e7d0-4a03-aa96-572ad1abc4e1",
                ClientSecret = "test_secret",
                TenantId = "common",
                LoginUrl = "https://login.microsoftonline.com"
            };

            // Act & Assert — não lança exceção
            ConfigurationValidator.ValidateTeamsSettings(settings);
        }

        [Fact]
        public void ValidateTeamsSettings_WithNullSettings_ThrowsConfigurationException()
        {
            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateTeamsSettings(null!));

            Assert.Contains("nulo", ex.Message);
        }

        [Fact]
        public void ValidateTeamsSettings_WithMissingBotId_ThrowsConfigurationException()
        {
            // Arrange
            var settings = new TeamsSettings
            {
                BotId = "",
                ClientSecret = "test",
                TenantId = "common",
                LoginUrl = "https://login.microsoftonline.com"
            };

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateTeamsSettings(settings));

            Assert.Contains("BotId", ex.Message);
        }

        [Fact]
        public void ValidateTeamsSettings_WithMissingClientSecret_ThrowsConfigurationException()
        {
            // Arrange
            var settings = new TeamsSettings
            {
                BotId = "0c4d4876-e7d0-4a03-aa96-572ad1abc4e1",
                ClientSecret = string.Empty,
                TenantId = "common",
                LoginUrl = "https://login.microsoftonline.com"
            };

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateTeamsSettings(settings));

            Assert.Contains("ClientSecret", ex.Message);
            Assert.Contains("dotnet user-secrets", ex.Message);
        }

        [Fact]
        public void ValidateTeamsSettings_WithMissingTenantId_ThrowsConfigurationException()
        {
            // Arrange
            var settings = new TeamsSettings
            {
                BotId = "0c4d4876-e7d0-4a03-aa96-572ad1abc4e1",
                ClientSecret = "test",
                TenantId = "",
                LoginUrl = "https://login.microsoftonline.com"
            };

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateTeamsSettings(settings));

            Assert.Contains("TenantId", ex.Message);
        }

        [Fact]
        public void ValidateTeamsSettings_WithMissingLoginUrl_ThrowsConfigurationException()
        {
            // Arrange
            var settings = new TeamsSettings
            {
                BotId = "0c4d4876-e7d0-4a03-aa96-572ad1abc4e1",
                ClientSecret = "test",
                TenantId = "common",
                LoginUrl = ""
            };

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateTeamsSettings(settings));

            Assert.Contains("LoginUrl", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateTeamsSettings_WithWhitespaceBotId_ThrowsConfigurationException(string? botId)
        {
            // Arrange
            var settings = new TeamsSettings
            {
                BotId = botId!,
                ClientSecret = "test",
                TenantId = "common",
                LoginUrl = "https://login.microsoftonline.com"
            };

            // Act & Assert
            Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateTeamsSettings(settings));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateTeamsSettings_WithWhitespaceClientSecret_ThrowsConfigurationException(string? secret)
        {
            // Arrange
            var settings = new TeamsSettings
            {
                BotId = "0c4d4876-e7d0-4a03-aa96-572ad1abc4e1",
                ClientSecret = secret!,
                TenantId = "common",
                LoginUrl = "https://login.microsoftonline.com"
            };

            // Act & Assert
            Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateTeamsSettings(settings));
        }
    }
}
