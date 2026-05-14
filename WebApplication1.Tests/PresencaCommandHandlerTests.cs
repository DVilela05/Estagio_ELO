using Moq;
using WebApplication1.Core.Commands;
using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;

namespace WebApplication1.Tests
{
    public class PresencaCommandHandlerTests
    {
        private readonly Mock<IBusinessApiClient> _mockApiClient;
        private readonly PresencaCommandHandler _handler;

        public PresencaCommandHandlerTests()
        {
            _mockApiClient = new Mock<IBusinessApiClient>();

            // Por defeito, o stub retorna sucesso (modo stub)
            _mockApiClient
                .Setup(c => c.RegisterAttendanceAsync(
                    It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(BusinessApiResult.Ok("Presença registada.", isStub: true));

            _handler = new PresencaCommandHandler(_mockApiClient.Object);
        }

        [Theory]
        [InlineData("presente")]
        [InlineData("presença")]
        [InlineData("presenca")]
        [InlineData("marcar presença")]
        [InlineData("cá estou")]
        [InlineData("estou cá")]
        [InlineData("cheguei")]
        [InlineData("present")]
        [InlineData("attendance")]
        [InlineData("mark attendance")]
        [InlineData("check in")]
        [InlineData("i'm here")]
        [InlineData("im here")]
        [InlineData("here")]
        [InlineData("arrived")]
        public void CanHandle_WithValidPresencaTrigger_ReturnsTrue(string trigger)
        {
            // Arrange
            var message = new IncomingMessage 
            { 
                Body = trigger.ToLowerInvariant(),
                OriginalBody = trigger
            };

            // Act
            var result = _handler.CanHandle(message);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("ajuda")]
        [InlineData("help")]
        [InlineData("random text")]
        public void CanHandle_WithInvalidTrigger_ReturnsFalse(string trigger)
        {
            // Arrange
            var message = new IncomingMessage 
            { 
                Body = trigger.ToLowerInvariant(),
                OriginalBody = trigger
            };

            // Act
            var result = _handler.CanHandle(message);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("PRESENTE")]
        [InlineData("Presente")]
        [InlineData("PRESENÇA")]
        public void CanHandle_WithDifferentCase_ReturnsTrue(string trigger)
        {
            // Arrange
            var message = new IncomingMessage 
            { 
                Body = trigger.ToLowerInvariant(),
                OriginalBody = trigger
            };

            // Act
            var result = _handler.CanHandle(message);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Execute_WithPresencaCommand_StubMode_ReturnsSuccessMessage()
        {
            // Arrange — mock já configurado no construtor (stub mode)
            var message = new IncomingMessage 
            { 
                Body = "presente",
                OriginalBody = "presente",
                From = "554199999999",
                UserId = "554199999999",
                UserName = "Diogo",
                HasLocation = true,
                Latitude = 41.1579,
                Longitude = -8.6291
            };

            // Act
            var result = await _handler.ExecuteAsync(message);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Contains("presença", result);
        }

        [Fact]
        public async Task Execute_WithPresencaCommand_RealMode_ReturnsSuccessMessage()
        {
            // Arrange — modo real (sem flag isStub)
            _mockApiClient
                .Setup(c => c.RegisterAttendanceAsync(
                    It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(BusinessApiResult.Ok("Presença registada."));

            var message = new IncomingMessage 
            { 
                Body = "presente",
                OriginalBody = "presente",
                From = "554199999999",
                UserId = "554199999999",
                UserName = "Diogo",
                HasLocation = true,
                Latitude = 41.1579,
                Longitude = -8.6291
            };

            // Act
            var result = await _handler.ExecuteAsync(message);

            // Assert
            Assert.Contains("sucesso", result);
        }

        [Fact]
        public async Task Execute_WhenApiReturnsTimeout_ReturnsTimeoutMessage()
        {
            // Arrange
            _mockApiClient
                .Setup(c => c.RegisterAttendanceAsync(
                    It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(BusinessApiResult.Timeout());

            var message = new IncomingMessage 
            { 
                Body = "presente",
                OriginalBody = "presente",
                UserId = "user1",
                UserName = "Test",
                HasLocation = true,
                Latitude = 41.1579,
                Longitude = -8.6291
            };

            // Act
            var result = await _handler.ExecuteAsync(message);

            // Assert
            Assert.Contains("demorou", result);
        }

        [Fact]
        public async Task Execute_WhenApiUnavailable_ReturnsUnavailableMessage()
        {
            // Arrange
            _mockApiClient
                .Setup(c => c.RegisterAttendanceAsync(
                    It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(BusinessApiResult.ServiceUnavailable());

            var message = new IncomingMessage 
            { 
                Body = "presente",
                OriginalBody = "presente",
                UserId = "user1",
                UserName = "Test",
                HasLocation = true,
                Latitude = 41.1579,
                Longitude = -8.6291
            };

            // Act
            var result = await _handler.ExecuteAsync(message);

            // Assert
            Assert.Contains("indisponível", result);
        }

        [Fact]
        public async Task Execute_WhenApiReturnsGenericError_ReturnsGenericErrorMessage()
        {
            // Arrange
            _mockApiClient
                .Setup(c => c.RegisterAttendanceAsync(
                    It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(BusinessApiResult.Fail("Erro interno", "InternalServerError"));

            var message = new IncomingMessage 
            { 
                Body = "presente",
                OriginalBody = "presente",
                UserId = "user1",
                UserName = "Test",
                HasLocation = true,
                Latitude = 41.1579,
                Longitude = -8.6291
            };

            // Act
            var result = await _handler.ExecuteAsync(message);

            // Assert
            Assert.Contains("problema", result);
        }

        [Fact]
        public async Task Execute_CallsApiWithCorrectParameters()
        {
            // Arrange
            _mockApiClient
                .Setup(c => c.RegisterAttendanceAsync(
                    "user123", "Diogo", "presente", "WhatsApp", null, null))
                .ReturnsAsync(BusinessApiResult.Ok("OK"))
                .Verifiable();

            var message = new IncomingMessage 
            { 
                Body = "presente",
                OriginalBody = "presente",
                UserId = "user123",
                UserName = "Diogo",
                Platform = MessagePlatform.WhatsApp,
                HasLocation = true,
                Latitude = 41.1579,
                Longitude = -8.6291
            };

            // Act
            await _handler.ExecuteAsync(message);

            // Assert — verifica que o API client foi chamado com os params certos
            _mockApiClient.Verify(c => c.RegisterAttendanceAsync(
                "user123", "Diogo", "presente", "WhatsApp", null, null), Times.Once);
        }

        [Fact]
        public void CommandName_ReturnsPresencaName()
        {
            // Act
            var name = _handler.CommandName;

            // Assert
            Assert.NotNull(name);
            Assert.Equal("presença", name);
        }

        [Fact]
        public void Description_ReturnsPresencaDescription()
        {
            // Act
            var description = _handler.Description;

            // Assert
            Assert.NotNull(description);
            Assert.NotEmpty(description);
        }

        [Fact]
        public void Triggers_ContainsValidTriggers()
        {
            // Act
            var triggers = _handler.Triggers;

            // Assert
            Assert.NotNull(triggers);
            Assert.NotEmpty(triggers);
            Assert.Contains("presente", triggers);
        }
    }
}
