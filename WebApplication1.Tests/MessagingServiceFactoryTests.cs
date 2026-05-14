using WebApplication1.Infrastructure.Messaging;
using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;

namespace WebApplication1.Tests
{
    public class MessagingServiceFactoryTests
    {
        [Fact]
        public void MessagingServiceFactory_WithEmptyServices_CanBeInstantiated()
        {
            // Arrange
            var services = new List<IMessagingService>();

            // Act
            var factory = new MessagingServiceFactory(services);

            // Assert
            Assert.NotNull(factory);
        }

        [Fact]
        public void MessagingServiceFactory_WithServices_StoresServices()
        {
            // Arrange & Act
            var services = new List<IMessagingService>();
            var factory = new MessagingServiceFactory(services);

            // Assert
            Assert.NotNull(factory);
        }
    }
}
