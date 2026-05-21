using Microsoft.Extensions.Logging;
using Moq;
using WebApplication1.Core.Localization;
using WebApplication1.Core.Models;

namespace WebApplication1.Tests
{
    public class LanguageDetectorTests
    {
        private readonly LanguageDetector _detector;
        private readonly IBotLocalizer _localizer;

        public LanguageDetectorTests()
        {
            _localizer = new BotLocalizer();
            _detector = new LanguageDetector(_localizer);
        }

        [Theory]
        [InlineData("present", SupportedLanguage.English)]
        [InlineData("présent", SupportedLanguage.French)]
        [InlineData("presente", SupportedLanguage.Portuguese)] // O trigger 'presente' existe em PT e ES, mas o PT tem prioridade na falta de indicativo
        [InlineData("aide", SupportedLanguage.French)]
        public void DetectLanguage_ByTrigger_ReturnsExpectedLanguage(string trigger, SupportedLanguage expectedLanguage)
        {
            // Arrange
            var msg = new IncomingMessage { Body = trigger, Platform = MessagePlatform.WhatsApp, From = "999999999" };

            // Act
            var lang = _detector.DetectLanguage(msg);

            // Assert
            Assert.Equal(expectedLanguage, lang);
        }

        [Theory]
        [InlineData("33612345678", SupportedLanguage.French)]
        [InlineData("447712345678", SupportedLanguage.English)]
        [InlineData("34612345678", SupportedLanguage.Spanish)]
        [InlineData("351912345678", SupportedLanguage.Portuguese)]
        public void DetectLanguage_ByPhoneIndicative_WhenTriggerIsUnknown(string phone, SupportedLanguage expectedLanguage)
        {
            // Arrange
            var msg = new IncomingMessage { Body = "unknown_command", Platform = MessagePlatform.WhatsApp, From = phone };

            // Act
            var lang = _detector.DetectLanguage(msg);

            // Assert
            Assert.Equal(expectedLanguage, lang);
        }

        [Fact]
        public void DetectLanguage_TeamsWithoutTrigger_ReturnsNull()
        {
            // Arrange
            var msg = new IncomingMessage { Body = "unknown", Platform = MessagePlatform.Teams, From = "diogo@elo.pt" };

            // Act
            var lang = _detector.DetectLanguage(msg);

            // Assert
            Assert.Null(lang);
        }
    }
}
