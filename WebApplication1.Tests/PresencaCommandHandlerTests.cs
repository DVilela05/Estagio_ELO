using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Polly;
using WebApplication1.Core.Commands;
using WebApplication1.Core.Localization;
using WebApplication1.Core.Models;

namespace WebApplication1.Tests
{
    public class PresencaCommandHandlerTests
    {
        private readonly PresencaCommandHandler _handler;
        private readonly IBotLocalizer _localizer;

        public PresencaCommandHandlerTests()
        {
            _localizer = new BotLocalizer();
            var loggerMock = new Mock<ILogger<PresencaCommandHandler>>();
            
            var options = Options.Create(new WebApplication1.Infrastructure.Configuration.WebServiceSettings());

            _handler = new PresencaCommandHandler(options, loggerMock.Object, _localizer);
        }

        [Theory]
        [InlineData("presente")]
        [InlineData("present")]
        [InlineData("présent")]
        [InlineData("here")]
        public void CanHandle_WithValidMultiLangTrigger_ReturnsTrue(string trigger)
        {
            // Arrange
            var message = new IncomingMessage { Body = trigger.ToLowerInvariant(), OriginalBody = trigger };

            // Act
            var result = _handler.CanHandle(message);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WithInvalidTrigger_ReturnsFalse()
        {
            // Arrange
            var message = new IncomingMessage { Body = "ajuda" };

            // Act
            var result = _handler.CanHandle(message);

            // Assert
            Assert.False(result);
        }
    }
}
