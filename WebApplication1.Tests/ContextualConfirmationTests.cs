using WebApplication1.Application;
using WebApplication1.Core.Commands;
using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace WebApplication1.Tests
{
    /// <summary>
    /// Testes para as confirmações contextuais — as respostas do bot
    /// devem referenciar o comando específico (presença, ajuda, etc.).
    /// </summary>
    public class ContextualConfirmationTests
    {
        private readonly Mock<ILogger<MessageProcessingService>> _loggerMock;
        private readonly Mock<ILogger<CommandRouter>> _routerLoggerMock;

        public ContextualConfirmationTests()
        {
            _loggerMock = new Mock<ILogger<MessageProcessingService>>();
            _routerLoggerMock = new Mock<ILogger<CommandRouter>>();
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

        // =====================================================================
        // Confirmações contextuais — comando "presença"
        // =====================================================================

        [Fact]
        public async Task ProcessMessage_PresencaCommand_ConfirmationShouldMentionPresenca()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            string capturedReply = "";
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => capturedReply = text)
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            var msg = new IncomingMessage
            {
                MessageId = $"ctx-pres-{Guid.NewGuid()}",
                From = $"ctx-user-pres-{Guid.NewGuid()}",
                UserId = "ctx-user-pres",
                Body = "presente",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            };

            // Act
            await service.ProcessMessageAsync(msg, mockService.Object);

            // Assert — a confirmação deve mencionar "presença"
            Assert.Contains("presença", capturedReply, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ProcessMessage_PresencaCommand_ConfirmationShouldContainSimNao()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            string capturedReply = "";
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => capturedReply = text)
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            var msg = new IncomingMessage
            {
                MessageId = $"ctx-simnao-{Guid.NewGuid()}",
                From = $"ctx-user-simnao-{Guid.NewGuid()}",
                UserId = "ctx-user-simnao",
                Body = "presente",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            };

            // Act
            await service.ProcessMessageAsync(msg, mockService.Object);

            // Assert — deve incluir (sim/não)
            Assert.Contains("sim/não", capturedReply, StringComparison.OrdinalIgnoreCase);
        }

        // =====================================================================
        // Cancelamento contextual — mensagem de cancelamento menciona o comando
        // =====================================================================

        [Fact]
        public async Task ProcessMessage_CancelConfirmation_ShouldMentionCommandName()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            var replies = new List<string>();
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => replies.Add(text))
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string userId = $"ctx-cancel-{Guid.NewGuid()}";

            // Step 1: enviar "presente" para criar confirmação pendente
            var msg1 = new IncomingMessage
            {
                MessageId = $"ctx-cancel1-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "presente",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            };
            await service.ProcessMessageAsync(msg1, mockService.Object);

            // Limpar anti-spam (cooldown+lock) para o step seguinte — preserva confirmação pendente
            MessageProcessingService.ResetUserState(userId);

            // Step 2: enviar "não" para cancelar
            var msg2 = new IncomingMessage
            {
                MessageId = $"ctx-cancel2-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "não",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            };
            await service.ProcessMessageAsync(msg2, mockService.Object);

            // Assert — a resposta de cancelamento deve mencionar "presença"
            string cancelReply = replies.Last();
            Assert.Contains("presença", cancelReply, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("cancelado", cancelReply, StringComparison.OrdinalIgnoreCase);
        }

        // =====================================================================
        // Mensagem não-sim/não mostra ajuda contextual (sem limite de tentativas)
        // =====================================================================

        [Fact]
        public async Task ProcessMessage_NonSimNaoResponse_ShouldShowHelpWithAttemptsRemaining()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            var replies = new List<string>();
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => replies.Add(text))
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string userId = $"ctx-invalid-{Guid.NewGuid()}";

            // Step 1: enviar "presente" → confirmação pendente
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-inv1-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "presente",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            Assert.Single(replies);
            Assert.Contains("sim/não", replies[0], StringComparison.OrdinalIgnoreCase);

            // Step 2: enviar "talvez" (não é sim/não) → ajuda contextual
            MessageProcessingService.ResetUserState(userId);

            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-inv2-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "talvez",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            // Assert — a segunda resposta continua no contexto de confirmação sim/não
            Assert.Equal(2, replies.Count);
            string helpReply = replies.Last();
            // Deve mencionar o comando pendente (presença) e pedir sim/não
            Assert.Contains("presença", helpReply, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                helpReply.Contains("sim", StringComparison.OrdinalIgnoreCase) &&
                (helpReply.Contains("não", StringComparison.OrdinalIgnoreCase) || helpReply.Contains("nao", StringComparison.OrdinalIgnoreCase)),
                $"Resposta de ajuda devia pedir SIM/NÃO. Reply: {helpReply}");
            Assert.Contains("Tentativas restantes", helpReply, StringComparison.OrdinalIgnoreCase);
        }

        // =====================================================================
        // Comando válido durante confirmação: mostra ajuda (não inicia novo comando)
        // =====================================================================

        [Fact]
        public async Task ProcessMessage_ValidCommandDuringConfirmation_ShouldNotCreateNewPrompt()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            var replies = new List<string>();
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => replies.Add(text))
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string userId = $"ctx-cancel-new-{Guid.NewGuid()}";
            DateTime baseTime = DateTime.UtcNow;

            // Step 1: "presente" → confirmação
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-cn1-{Guid.NewGuid()}", From = userId,
                UserId = userId, Body = "presente",
                Platform = MessagePlatform.WhatsApp, ReceivedAt = baseTime,
                SentAt = baseTime
            }, mockService.Object);

            // Step 2: "presente" novamente → não deve criar novo prompt de confirmação
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-cn2-{Guid.NewGuid()}", From = userId,
                UserId = userId, Body = "presente",
                Platform = MessagePlatform.WhatsApp, ReceivedAt = baseTime.AddMilliseconds(200),
                SentAt = baseTime.AddMilliseconds(200)
            }, mockService.Object);

            // Assert — mantém apenas a confirmação inicial (não cria prompt novo)
            Assert.Single(replies);
            Assert.Contains("sim/não", replies[0], StringComparison.OrdinalIgnoreCase);

            // Cleanup
            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public async Task ProcessMessage_ThirdInvalidAttempt_ShouldExplainExpectedAndSuggestHelp()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            var replies = new List<string>();
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => replies.Add(text))
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string userId = $"ctx-3invalid-help-{Guid.NewGuid()}";

            // Step 1: cria confirmação pendente
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-3invh-1-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "presente",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            // Step 2/3: tentativas inválidas (1 e 2)
            MessageProcessingService.ResetUserState(userId);
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-3invh-2-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "talvez",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            MessageProcessingService.ResetUserState(userId);
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-3invh-3-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "xyz",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            // Step 4: tentativa inválida 3 → resposta final esperada
            MessageProcessingService.ResetUserState(userId);
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-3invh-4-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "blah",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            // Assert
            Assert.True(replies.Count >= 4);
            string finalReply = replies.Last();
            string normalized = finalReply.ToLowerInvariant();
            Assert.DoesNotContain("Tentativas restantes", finalReply, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ajuda", normalized);
            Assert.True(
                normalized.Contains("sim") && (normalized.Contains("não") || normalized.Contains("nao")),
                $"Resposta final não explicou o esperado sim/não. Reply: {finalReply}");

            // Cleanup
            MessageProcessingService.ResetUserState(userId);
        }

        // =====================================================================
        // Múltiplas tentativas inválidas de localização → continuam a pedir a localização
        // =====================================================================

        [Fact]
        public async Task ProcessMessage_MultipleInvalidLocationAttempts_ShouldKeepRequestAlive()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            var replies = new List<string>();
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => replies.Add(text))
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string userId = $"ctx-3att-{Guid.NewGuid()}";

            // Step 1: "presente" → confirmação pendente
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-3att1-{Guid.NewGuid()}", From = userId,
                UserId = userId, Body = "presente",
                Platform = MessagePlatform.WhatsApp, ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);
            Assert.Single(replies);
            Assert.Contains("sim/não", replies[0], StringComparison.OrdinalIgnoreCase);

            // Step 2: confirmar com "sim" para entrar no estágio de localização
            MessageProcessingService.ResetUserState(userId);
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-3att2-{Guid.NewGuid()}", From = userId,
                UserId = userId, Body = "sim",
                Platform = MessagePlatform.WhatsApp, ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);
            Assert.Equal(2, replies.Count);
            Assert.True(
                replies[1].Contains("localização", StringComparison.OrdinalIgnoreCase) ||
                replies[1].Contains("pin", StringComparison.OrdinalIgnoreCase),
                $"Pedido inicial devia mencionar localização ou PIN. Reply: {replies[1]}");

            // Step 3: tentativa inválida 1 de localização → ajuda contextual de localização
            MessageProcessingService.ResetUserState(userId);
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-3att3-{Guid.NewGuid()}", From = userId,
                UserId = userId, Body = "talvez",
                Platform = MessagePlatform.WhatsApp, ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);
            Assert.Equal(3, replies.Count);
            Assert.True(
                replies[2].Contains("localização", StringComparison.OrdinalIgnoreCase) ||
                replies[2].Contains("pin", StringComparison.OrdinalIgnoreCase),
                $"Resposta devia mencionar localização ou PIN. Reply: {replies[2]}");
            Assert.DoesNotContain("Tentativas restantes", replies[2], StringComparison.OrdinalIgnoreCase);

            // Step 4: tentativa inválida 2 → continua a pedir localização
            MessageProcessingService.ResetUserState(userId);
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-3att4-{Guid.NewGuid()}", From = userId,
                UserId = userId, Body = "xyz",
                Platform = MessagePlatform.WhatsApp, ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);
            Assert.Equal(4, replies.Count);
            Assert.True(
                replies[3].Contains("localização", StringComparison.OrdinalIgnoreCase) ||
                replies[3].Contains("pin", StringComparison.OrdinalIgnoreCase),
                $"Resposta devia mencionar localização ou PIN. Reply: {replies[3]}");
            Assert.DoesNotContain("Tentativas restantes", replies[3], StringComparison.OrdinalIgnoreCase);

            // Step 5: tentativa inválida 3 → continua a pedir localização
            MessageProcessingService.ResetUserState(userId);
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-3att5-{Guid.NewGuid()}", From = userId,
                UserId = userId, Body = "blah",
                Platform = MessagePlatform.WhatsApp, ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            // Assert — continua a pedir localização, sem cancelar por número fixo de tentativas
            Assert.Equal(5, replies.Count);
            string finalReply = replies[4];
            Assert.True(
                finalReply.Contains("localização", StringComparison.OrdinalIgnoreCase) ||
                finalReply.Contains("pin", StringComparison.OrdinalIgnoreCase),
                $"Resposta final devia mencionar localização ou PIN. Reply: {finalReply}");
            Assert.DoesNotContain("cancel", finalReply, StringComparison.OrdinalIgnoreCase);

            // Cleanup
            MessageProcessingService.ResetUserState(userId);
        }

        // =====================================================================
        // Confirmação com SIM em presença → entra em estágio de localização
        // =====================================================================

        [Fact]
        public async Task ProcessMessage_ConfirmWithYesForPresenca_ShouldAskForLocationPin()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            var replies = new List<string>();
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => replies.Add(text))
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string userId = $"ctx-yes-{Guid.NewGuid()}";

            // Step 1: "presente"
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-yes1-{Guid.NewGuid()}", From = userId,
                UserId = userId, Body = "presente",
                Platform = MessagePlatform.WhatsApp, ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            // Limpar anti-spam (cooldown+lock) para o step seguinte — preserva confirmação pendente
            MessageProcessingService.ResetUserState(userId);

            // Step 2: "sim"
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-yes2-{Guid.NewGuid()}", From = userId,
                UserId = userId, Body = "sim",
                Platform = MessagePlatform.WhatsApp, ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            // Assert — segunda resposta deve pedir localização
            string confirmReply = replies.Last();
            Assert.Contains("localização", confirmReply, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("presença", confirmReply, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ProcessMessage_PresencaYesThenLocation_ShouldCompleteSuccessfully()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            var replies = new List<string>();
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => replies.Add(text))
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string userId = $"ctx-loc-ok-{Guid.NewGuid()}";

            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-loc-ok-1-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "presente",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            MessageProcessingService.ResetUserState(userId);

            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-loc-ok-2-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "sim",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            MessageProcessingService.ResetUserState(userId);

            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-loc-ok-3-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "",
                OriginalBody = "",
                Platform = MessagePlatform.WhatsApp,
                HasLocation = true,
                Latitude = 38.722300,
                Longitude = -9.139300,
                LocationName = "Lisboa",
                LocationAddress = "Lisboa, Portugal",
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            Assert.True(replies.Count >= 3);
            string finalReply = replies.Last();
            Assert.Contains("PIN de localização recebido", finalReply, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("(38.722300, -9.139300)", finalReply, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Lisboa, Portugal", finalReply, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sucesso", finalReply, StringComparison.OrdinalIgnoreCase);

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public async Task ProcessMessage_PresencaYesThenWebText_ShouldStillRequireLocationPin()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            var replies = new List<string>();
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => replies.Add(text))
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string userId = $"ctx-loc-web-{Guid.NewGuid()}";

            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-loc-web-1-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "presente",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            MessageProcessingService.ResetUserState(userId);

            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-loc-web-2-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "sim",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            MessageProcessingService.ResetUserState(userId);

            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-loc-web-3-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "Web",
                OriginalBody = "Web",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            Assert.True(replies.Count >= 3);
            string finalReply = replies.Last();
            Assert.Contains("localização", finalReply, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Tentativas restantes", finalReply, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sucesso", finalReply, StringComparison.OrdinalIgnoreCase);

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public async Task ProcessMessage_PresencaYesWithoutLocation_MultipleAttempts_ShouldKeepPrompting()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            var replies = new List<string>();
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => replies.Add(text))
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string userId = $"ctx-loc-cancel-{Guid.NewGuid()}";

            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-loc-cancel-1-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "presente",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            MessageProcessingService.ResetUserState(userId);

            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-loc-cancel-2-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "sim",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            for (int i = 0; i < 3; i++)
            {
                MessageProcessingService.ResetUserState(userId);
                await service.ProcessMessageAsync(new IncomingMessage
                {
                    MessageId = $"ctx-loc-cancel-x-{i}-{Guid.NewGuid()}",
                    From = userId,
                    UserId = userId,
                    Body = i == 0 ? "ok" : i == 1 ? "já enviei" : "ainda não",
                    Platform = MessagePlatform.WhatsApp,
                    ReceivedAt = DateTime.Now,
                    SentAt = DateTime.UtcNow
                }, mockService.Object);
            }

            string finalReply = replies.Last();
            Assert.Contains("localização", finalReply, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Tentativas restantes", finalReply, StringComparison.OrdinalIgnoreCase);

            MessageProcessingService.ResetUserState(userId);
        }

        // =====================================================================
        // Confirmação funciona via Teams (mesma lógica, plataforma diferente)
        // =====================================================================

        [Fact]
        public async Task ProcessMessage_TeamsPresenca_ConfirmationShouldMentionPresenca()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            string capturedReply = "";
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => capturedReply = text)
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            var msg = new IncomingMessage
            {
                MessageId = $"ctx-teams-{Guid.NewGuid()}",
                From = $"ctx-teams-endpoint-{Guid.NewGuid()}",
                UserId = "teams-user-001",
                UserName = "Diogo",
                Body = "presente",
                Platform = MessagePlatform.Teams,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            };

            // Act
            await service.ProcessMessageAsync(msg, mockService.Object);

            // Assert — funciona igual para Teams
            Assert.Contains("presença", capturedReply, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sim/não", capturedReply, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ProcessMessage_TeamsPresenca_ConfirmationShouldIncludeQuotedCitation()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            string capturedReply = "";
            string? capturedReplyTo = null;
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, replyTo) =>
                {
                    capturedReply = text;
                    capturedReplyTo = replyTo;
                })
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string messageId = $"ctx-teams-quote-{Guid.NewGuid()}";
            var msg = new IncomingMessage
            {
                MessageId = messageId,
                From = $"ctx-teams-endpoint-{Guid.NewGuid()}",
                UserId = "teams-user-quote-001",
                UserName = "Diogo",
                Body = "presente",
                OriginalBody = "presente no teams",
                Platform = MessagePlatform.Teams,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            };

            // Act
            await service.ProcessMessageAsync(msg, mockService.Object);

            // Assert
            Assert.StartsWith("> presente no teams", capturedReply, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sim/não", capturedReply, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(messageId, capturedReplyTo);
        }

        [Fact]
        public async Task ProcessMessage_TeamsPendingConfirmation_InvalidAttempts_ShouldQuoteCurrentUserReply()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            var replies = new List<string>();
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, replyTo) => replies.Add(text))
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string userId = $"ctx-teams-quote-flow-{Guid.NewGuid()}";

            // Step 1: comando válido cria confirmação pendente
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-teams-q1-{Guid.NewGuid()}",
                From = userId,
                UserId = "teams-user-quote-flow",
                UserName = "Diogo",
                Body = "presente",
                OriginalBody = "presente",
                Platform = MessagePlatform.Teams,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            MessageProcessingService.ResetUserState(userId);

            // Step 2: tentativa inválida 1
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-teams-q2-{Guid.NewGuid()}",
                From = userId,
                UserId = "teams-user-quote-flow",
                UserName = "Diogo",
                Body = "boas",
                OriginalBody = "boas",
                Platform = MessagePlatform.Teams,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            MessageProcessingService.ResetUserState(userId);

            // Step 3: tentativa inválida 2
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-teams-q3-{Guid.NewGuid()}",
                From = userId,
                UserId = "teams-user-quote-flow",
                UserName = "Diogo",
                Body = "olá",
                OriginalBody = "olá",
                Platform = MessagePlatform.Teams,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            // Assert
            Assert.True(replies.Count >= 3);
            Assert.StartsWith("> boas", replies[1], StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("> olá", replies[2], StringComparison.OrdinalIgnoreCase);

            // Cleanup
            MessageProcessingService.ResetUserState(userId);
        }

        // =====================================================================
        // Bypass anti-spam — "sim" imediato após confirmação (cenário real)
        // =====================================================================

        /// <summary>
        /// Cenário real: utilizador envia "presente", bot pergunta sim/não,
        /// utilizador responde "sim" imediatamente — SEM ResetUserState.
        /// O bypass de confirmação pendente deve permitir a resposta mesmo
        /// com cooldown e lock ativos (bug corrigido).
        /// </summary>
        [Fact]
        public async Task ProcessMessage_ImmediateSimAfterConfirmation_ShouldBypassAntiSpam()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            var replies = new List<string>();
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => replies.Add(text))
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string userId = $"ctx-bypass-{Guid.NewGuid()}";

            // Step 1: "presente" → bot responde com confirmação sim/não
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-bypass1-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "presente",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            Assert.Single(replies);
            Assert.Contains("sim/não", replies[0], StringComparison.OrdinalIgnoreCase);

            // Step 2: "sim" — SEM ResetUserState (cenário real: cooldown + lock ativos)
            // O bypass de confirmação pendente deve permitir esta resposta
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-bypass2-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "sim",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            // Assert — devem haver 2 respostas: confirmação + pedido de localização
            Assert.Equal(2, replies.Count);
            Assert.Contains("localização", replies[1], StringComparison.OrdinalIgnoreCase);

            // Cleanup
            MessageProcessingService.ResetUserState(userId);
        }

        /// <summary>
        /// Cenário real: utilizador envia "presente", bot pergunta sim/não,
        /// utilizador responde "não" imediatamente — SEM ResetUserState.
        /// O cancelamento deve funcionar mesmo com anti-spam ativo.
        /// </summary>
        [Fact]
        public async Task ProcessMessage_ImmediateNaoAfterConfirmation_ShouldBypassAntiSpam()
        {
            // Arrange
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);

            var replies = new List<string>();
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string?>((to, text, _replyTo) => replies.Add(text))
                .ReturnsAsync(true);

            var presencaHandler = new PresencaCommandHandler(CreateStubBusinessApiClient());
            var handlers = new ICommandHandler[] { presencaHandler };
            var router = new CommandRouter(handlers, _routerLoggerMock.Object);
            var service = new MessageProcessingService(router, _loggerMock.Object);

            string userId = $"ctx-bypass-nao-{Guid.NewGuid()}";

            // Step 1: "presente" → confirmação
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-bpn1-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "presente",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            Assert.Single(replies);

            // Step 2: "não" imediato — SEM ResetUserState
            await service.ProcessMessageAsync(new IncomingMessage
            {
                MessageId = $"ctx-bpn2-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "não",
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.Now,
                SentAt = DateTime.UtcNow
            }, mockService.Object);

            // Assert — cancelamento deve funcionar
            Assert.Equal(2, replies.Count);
            Assert.Contains("cancelado", replies[1], StringComparison.OrdinalIgnoreCase);

            // Cleanup
            MessageProcessingService.ResetUserState(userId);
        }
    }
}
