using Moq;
using WebApplication1.Core.Commands;
using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace WebApplication1.Tests
{
    public class CommandRouterTests
    {
        [Fact]
        public void IsValidCommand_WithValidPresencaCommand_ReturnsTrue()
        {
            // Arrange
            var handlers = new List<ICommandHandler>
            {
                new HelpCommandHandler(new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider()),
                new PresencaCommandHandler(CreateStubBusinessApiClient())
            };
            
            var loggerMock = new Mock<ILogger<CommandRouter>>();
            var router = new CommandRouter(handlers.ToArray(), loggerMock.Object);

            var message = new IncomingMessage 
            { 
                Body = "presente", 
                OriginalBody = "presente" 
            };

            // Act
            var result = router.IsValidCommand(message);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidCommand_WithInvalidCommand_ReturnsFalse()
        {
            // Arrange
            var handlers = new List<ICommandHandler>
            {
                new HelpCommandHandler(new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider()),
                new PresencaCommandHandler(CreateStubBusinessApiClient())
            };
            
            var loggerMock = new Mock<ILogger<CommandRouter>>();
            var router = new CommandRouter(handlers.ToArray(), loggerMock.Object);

            var message = new IncomingMessage 
            { 
                Body = "random text that is not a command", 
                OriginalBody = "random text that is not a command" 
            };

            // Act
            var result = router.IsValidCommand(message);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void TryGetMatchedHandler_WithValidPresencaCommand_ReturnPresencaHandler()
        {
            // Arrange
            var handlers = new List<ICommandHandler>
            {
                new HelpCommandHandler(new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider()),
                new PresencaCommandHandler(CreateStubBusinessApiClient())
            };
            
            var loggerMock = new Mock<ILogger<CommandRouter>>();
            var router = new CommandRouter(handlers.ToArray(), loggerMock.Object);

            var message = new IncomingMessage 
            { 
                Body = "presente", 
                OriginalBody = "presente" 
            };

            // Act
            var result = router.TryGetMatchedHandler(message, out var handler);

            // Assert
            Assert.True(result);
            Assert.NotNull(handler);
            Assert.IsType<PresencaCommandHandler>(handler);
        }

        [Fact]
        public async Task RouteAsync_WithValidCommand_CallsHandler()
        {
            // Arrange
            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new List<ICommandHandler> { presencaHandler };
            
            var loggerMock = new Mock<ILogger<CommandRouter>>();
            var router = new CommandRouter(handlers.ToArray(), loggerMock.Object);

            var message = new IncomingMessage 
            { 
                Body = "presente",
                OriginalBody = "presente",
                From = "554199999999"
            };

            // Act
            var result = await router.RouteAsync(message);

            // Assert
            Assert.NotNull(result);
        }

        private static IBusinessApiClient CreateStubBusinessApiClient()
        {
            var mock = new Mock<IBusinessApiClient>();
            mock.Setup(c => c.RegisterAttendanceAsync(
                    It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(BusinessApiResult.Ok("OK", isStub: true));
            return mock.Object;
        }
    }
}
