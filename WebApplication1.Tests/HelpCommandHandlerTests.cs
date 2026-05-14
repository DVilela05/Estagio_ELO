using WebApplication1.Core.Commands;
using WebApplication1.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace WebApplication1.Tests
{
    public class HelpCommandHandlerTests
    {
        private sealed class FakeAdminHandler : ICommandHandler
        {
            public string CommandName => "adminMenu";
            public string Description => "Admin hidden";
            public string[] Triggers => new[] { "adminMenu", "adminPing", "adminConfig" };
            public bool CanHandle(IncomingMessage message) => false;
            public Task<string> ExecuteAsync(IncomingMessage message) => Task.FromResult("ok");
        }

        [Theory]
        [InlineData("ajuda")]
        [InlineData("help")]
        [InlineData("?")]
        [InlineData("menu")]
        [InlineData("command")]
        [InlineData("commands")]
        public void CanHandle_WithValidTrigger_ReturnsTrue(string trigger)
        {
            // Arrange
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();

            var handler = new HelpCommandHandler(serviceProvider);
            var message = new IncomingMessage 
            { 
                Body = trigger.ToLowerInvariant(),
                OriginalBody = trigger
            };

            // Act
            var result = handler.CanHandle(message);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("random text")]
        [InlineData("hello")]
        [InlineData("xyz")]
        public void CanHandle_WithInvalidTrigger_ReturnsFalse(string trigger)
        {
            // Arrange
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();

            var handler = new HelpCommandHandler(serviceProvider);
            var message = new IncomingMessage 
            { 
                Body = trigger.ToLowerInvariant(),
                OriginalBody = trigger
            };

            // Act
            var result = handler.CanHandle(message);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Execute_WithHelpCommand_ReturnsHelpText()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<ICommandHandler>(new FakeAdminHandler());
            var serviceProvider = services.BuildServiceProvider();

            var handler = new HelpCommandHandler(serviceProvider);
            var message = new IncomingMessage 
            { 
                Body = "ajuda",
                OriginalBody = "ajuda",
                From = "554199999999"
            };

            // Act
            var result = await handler.ExecuteAsync(message);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.DoesNotContain("adminMenu", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("adminPing", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("adminConfig", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CommandName_ReturnsHelpName()
        {
            // Arrange
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var handler = new HelpCommandHandler(serviceProvider);

            // Act
            var name = handler.CommandName;

            // Assert
            Assert.NotNull(name);
            Assert.Equal("ajuda", name);
        }

        [Fact]
        public void Description_ReturnsHelpDescription()
        {
            // Arrange
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var handler = new HelpCommandHandler(serviceProvider);

            // Act
            var description = handler.Description;

            // Assert
            Assert.NotNull(description);
            Assert.NotEmpty(description);
        }
    }
}
