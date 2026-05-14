using WebApplication1.Infrastructure.Configuration;
using WebApplication1.Core.Exceptions;

namespace WebApplication1.Tests
{
    public class ConfigurationValidatorTests
    {
        [Fact]
        public void ValidateBusinessApiSettings_WithValidDevelopmentConfig_DoesNotThrow()
        {
            var settings = new BusinessApiSettings
            {
                BaseUrl = "http://localhost:5008",
                AttendancePath = "/api/attendance",
                ServiceToken = "token",
                HmacSecret = "secret",
                AllowInsecureHttp = true
            };

            ConfigurationValidator.ValidateBusinessApiSettings(settings, isDevelopment: true);
        }

        [Fact]
        public void ValidateBusinessApiSettings_WithMissingSecuritySecrets_ThrowsConfigurationException()
        {
            var settings = new BusinessApiSettings
            {
                BaseUrl = "http://localhost:5008",
                AttendancePath = "/api/attendance",
                AllowInsecureHttp = true
            };

            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateBusinessApiSettings(settings, isDevelopment: true));

            Assert.Contains("ServiceToken", ex.Message);
            Assert.Contains("HmacSecret", ex.Message);
        }

        [Fact]
        public void ValidateBusinessApiSettings_WithHttpInProduction_ThrowsConfigurationException()
        {
            var settings = new BusinessApiSettings
            {
                BaseUrl = "http://localhost:5008",
                AttendancePath = "/api/attendance",
                ServiceToken = "token",
                HmacSecret = "secret",
                AllowInsecureHttp = false
            };

            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateBusinessApiSettings(settings, isDevelopment: false));

            Assert.Contains("HTTPS", ex.Message);
        }

        [Fact]
        public void ValidateWhatsAppSettings_WithValidSettings_DoesNotThrow()
        {
            // Arrange
            var settings = new WhatsAppSettings
            {
                VerifyToken = "test_token",
                AccessToken = "test_access",
                AppSecret = "test_secret",
                PhoneNumberId = "123456",
                ApiVersion = "v22.0"
            };

            // Act & Assert
            ConfigurationValidator.ValidateWhatsAppSettings(settings);
            // Se chegou aqui, passou ✅
        }

        [Fact]
        public void ValidateWhatsAppSettings_WithNullSettings_ThrowsConfigurationException()
        {
            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateWhatsAppSettings(null!));
            
            Assert.Contains("nulo", ex.Message);
        }

        [Fact]
        public void ValidateWhatsAppSettings_WithMissingVerifyToken_ThrowsConfigurationException()
        {
            // Arrange
            var settings = new WhatsAppSettings
            {
                VerifyToken = "",  // Empty
                AccessToken = "test",
                AppSecret = "test",
                PhoneNumberId = "123",
                ApiVersion = "v22.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateWhatsAppSettings(settings));
            
            Assert.Contains("VerifyToken", ex.Message);
        }

        [Fact]
        public void ValidateWhatsAppSettings_WithMissingAccessToken_ThrowsConfigurationException()
        {
            // Arrange
            var settings = new WhatsAppSettings
            {
                VerifyToken = "test",
                AccessToken = string.Empty,  // Missing
                AppSecret = "test",
                PhoneNumberId = "123",
                ApiVersion = "v22.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateWhatsAppSettings(settings));
            
            Assert.Contains("AccessToken", ex.Message);
            Assert.Contains("dotnet user-secrets", ex.Message);
        }

        [Fact]
        public void ValidateWhatsAppSettings_WithMissingAppSecret_ThrowsConfigurationException()
        {
            // Arrange
            var settings = new WhatsAppSettings
            {
                VerifyToken = "test",
                AccessToken = "test",
                AppSecret = "",  // Empty
                PhoneNumberId = "123",
                ApiVersion = "v22.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateWhatsAppSettings(settings));
            
            Assert.Contains("AppSecret", ex.Message);
        }

        [Fact]
        public void ValidateWhatsAppSettings_WithMissingPhoneNumberId_ThrowsConfigurationException()
        {
            // Arrange
            var settings = new WhatsAppSettings
            {
                VerifyToken = "test",
                AccessToken = "test",
                AppSecret = "test",
                PhoneNumberId = string.Empty,  // Missing
                ApiVersion = "v22.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateWhatsAppSettings(settings));
            
            Assert.Contains("PhoneNumberId", ex.Message);
        }

        [Fact]
        public void ValidateWhatsAppSettings_WithMissingApiVersion_ThrowsConfigurationException()
        {
            // Arrange
            var settings = new WhatsAppSettings
            {
                VerifyToken = "test",
                AccessToken = "test",
                AppSecret = "test",
                PhoneNumberId = "123",
                ApiVersion = ""  // Empty
            };

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateWhatsAppSettings(settings));
            
            Assert.Contains("ApiVersion", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateWhatsAppSettings_WithWhitespaceToken_ThrowsConfigurationException(string token)
        {
            // Arrange
            var settings = new WhatsAppSettings
            {
                VerifyToken = token,
                AccessToken = "test",
                AppSecret = "test",
                PhoneNumberId = "123",
                ApiVersion = "v22.0"
            };

            // Act & Assert
            Assert.Throws<ConfigurationException>(
                () => ConfigurationValidator.ValidateWhatsAppSettings(settings));
        }
    }
}
