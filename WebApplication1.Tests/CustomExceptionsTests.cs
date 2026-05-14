using WebApplication1.Core.Exceptions;

namespace WebApplication1.Tests
{
    public class CustomExceptionsTests
    {
        [Fact]
        public void InvalidCommandException_CanBeCreated()
        {
            // Act
            var ex = new InvalidCommandException("Comando inválido");

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("Comando inválido", ex.Message);
        }

        [Fact]
        public void InvalidCommandException_CanBeCreatedWithInnerException()
        {
            // Arrange
            var inner = new Exception("Inner error");

            // Act
            var ex = new InvalidCommandException("Outer error", inner);

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("Outer error", ex.Message);
            Assert.Equal(inner, ex.InnerException);
        }

        [Fact]
        public void WebhookVerificationException_CanBeCreated()
        {
            // Act
            var ex = new WebhookVerificationException("Assinatura inválida");

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("Assinatura inválida", ex.Message);
        }

        [Fact]
        public void ConfigurationException_CanBeCreated()
        {
            // Act
            var ex = new ConfigurationException("Configuração em falta");

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("Configuração em falta", ex.Message);
        }

        [Fact]
        public void MessageProcessingException_CanBeCreated()
        {
            // Act
            var ex = new MessageProcessingException("Erro ao processar mensagem");

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("Erro ao processar mensagem", ex.Message);
        }

        [Fact]
        public void Exceptions_InheritFromException()
        {
            // Assert
            Assert.True(new InvalidCommandException("test") is Exception);
            Assert.True(new WebhookVerificationException("test") is Exception);
            Assert.True(new ConfigurationException("test") is Exception);
            Assert.True(new MessageProcessingException("test") is Exception);
        }
    }
}
