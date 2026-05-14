using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Moq;
using WebApplication1.Core.Commands;
using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.Configuration;

namespace WebApplication1.Tests
{
    public class AdminCommandHandlerTests
    {
        private static AdminCommandHandler CreateHandler(
            Mock<IBusinessApiClient> businessApiClientMock,
            string environmentName = "Development")
        {
            var adminSettings = Options.Create(new AdminSettings
            {
                Enabled = true,
                AllowInDevelopmentWithoutWhitelist = true
            });

            var businessSettings = Options.Create(new BusinessApiSettings
            {
                BaseUrl = "http://localhost:5008",
                AttendancePath = "/api/attendance",
                AllowInsecureHttp = true,
                ServiceToken = "token",
                HmacSecret = "hmac"
            });

            var env = new Mock<IHostEnvironment>();
            env.SetupGet(x => x.EnvironmentName).Returns(environmentName);

            return new AdminCommandHandler(
                businessApiClientMock.Object,
                adminSettings,
                businessSettings,
                env.Object);
        }

        [Fact]
        public async Task AdminPing_WithoutPass_ShouldReturnFakeNotFoundReply()
        {
            var businessApiClient = new Mock<IBusinessApiClient>();
            var handler = CreateHandler(businessApiClient);

            var message = new IncomingMessage
            {
                From = "admin-user-1",
                UserId = "admin-user-1",
                Body = "adminPing",
                OriginalBody = "adminPing",
                Platform = MessagePlatform.Teams
            };

            var reply = await handler.ExecuteAsync(message);

            Assert.Contains("comando", reply, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ajuda", reply, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("palavra-passe", reply, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Pass_AfterAdminPing_ShouldExecutePing()
        {
            var businessApiClient = new Mock<IBusinessApiClient>();
            businessApiClient.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
            var handler = CreateHandler(businessApiClient);
            string userId = $"admin-user-{Guid.NewGuid()}";
            string code = $"admin{DateTime.Now:ddMMyyyy}";

            await handler.ExecuteAsync(new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = "adminPing",
                OriginalBody = "adminPing",
                Platform = MessagePlatform.Teams
            });

            var message = new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = code,
                OriginalBody = code,
                Platform = MessagePlatform.Teams
            };

            var reply = await handler.ExecuteAsync(message);

            Assert.Contains("Relatório técnico", reply, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("OK", reply, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Latência", reply, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AdminMenu_ShouldReturnFakeNotFound_UntilPasswordIsSent()
        {
            var businessApiClient = new Mock<IBusinessApiClient>();
            var handler = CreateHandler(businessApiClient);
            string userId = $"admin-user-{Guid.NewGuid()}";
            string code = $"admin{DateTime.Now:ddMMyyyy}";

            var firstReply = await handler.ExecuteAsync(new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = "adminMenu",
                OriginalBody = "adminMenu",
                Platform = MessagePlatform.Teams
            });

            Assert.Contains("comando", firstReply, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ajuda", firstReply, StringComparison.OrdinalIgnoreCase);

            var secondReply = await handler.ExecuteAsync(new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = code,
                OriginalBody = code,
                Platform = MessagePlatform.Teams
            });

            Assert.Contains("Menu de administrador", secondReply, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("1 ação", secondReply, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task MenuFlow_ShouldAllowOnlyOneActionWithoutPassword()
        {
            var businessApiClient = new Mock<IBusinessApiClient>();
            businessApiClient.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
            var handler = CreateHandler(businessApiClient);

            string userId = $"admin-user-{Guid.NewGuid()}";
            string code = $"admin{DateTime.Now:ddMMyyyy}";

            await handler.ExecuteAsync(new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = "adminMenu",
                OriginalBody = "adminMenu",
                Platform = MessagePlatform.Teams
            });

            await handler.ExecuteAsync(new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = code,
                OriginalBody = code,
                Platform = MessagePlatform.Teams
            });

            var firstAction = await handler.ExecuteAsync(new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = "adminPing",
                OriginalBody = "adminPing",
                Platform = MessagePlatform.Teams
            });

            Assert.Contains("Relatório técnico", firstAction, StringComparison.OrdinalIgnoreCase);

            var secondAction = await handler.ExecuteAsync(new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = "adminConfig",
                OriginalBody = "adminConfig",
                Platform = MessagePlatform.Teams
            });

            Assert.Contains("comando", secondAction, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ajuda", secondAction, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("palavra-passe", secondAction, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AfterExecutingOneCommand_NextCommandShouldRequirePassAgain()
        {
            var businessApiClient = new Mock<IBusinessApiClient>();
            businessApiClient.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);

            var handler = CreateHandler(businessApiClient);
            string userId = $"admin-user-{Guid.NewGuid()}";
            string code = $"admin{DateTime.Now:ddMMyyyy}";

            // Pedido do comando
            await handler.ExecuteAsync(new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = "adminPing",
                OriginalBody = "adminPing",
                Platform = MessagePlatform.Teams
            });

            // Execução com palavra-passe
            var firstExecution = await handler.ExecuteAsync(new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = code,
                OriginalBody = code,
                Platform = MessagePlatform.Teams
            });

            Assert.Contains("Relatório técnico", firstExecution, StringComparison.OrdinalIgnoreCase);

            // Novo comando sem pass tem de voltar a pedir pass
            var secondCommand = await handler.ExecuteAsync(new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = "adminConfig",
                OriginalBody = "adminConfig",
                Platform = MessagePlatform.Teams
            });

            Assert.Contains("comando", secondCommand, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ajuda", secondCommand, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("palavra-passe", secondCommand, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PassAndCommandInSameMessage_ShouldExecuteDirectly()
        {
            var businessApiClient = new Mock<IBusinessApiClient>();
            businessApiClient.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
            var handler = CreateHandler(businessApiClient);

            string text = $"admin{DateTime.Now:ddMMyyyy} adminPing";

            var reply = await handler.ExecuteAsync(new IncomingMessage
            {
                From = "admin-user-inline",
                UserId = "admin-user-inline",
                Body = text,
                OriginalBody = text,
                Platform = MessagePlatform.Teams
            });

            Assert.Contains("Relatório técnico", reply, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SlashPassword_ShouldNotBeAccepted()
        {
            var businessApiClient = new Mock<IBusinessApiClient>();
            var handler = CreateHandler(businessApiClient);
            string userId = $"admin-user-{Guid.NewGuid()}";
            string slashCode = $"admin{DateTime.Now:dd/MM/yyyy}";

            await handler.ExecuteAsync(new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = "adminPing",
                OriginalBody = "adminPing",
                Platform = MessagePlatform.Teams
            });

            var reply = await handler.ExecuteAsync(new IncomingMessage
            {
                From = userId,
                UserId = userId,
                Body = slashCode,
                OriginalBody = slashCode,
                Platform = MessagePlatform.Teams
            });

            Assert.DoesNotContain("Relatório técnico", reply, StringComparison.OrdinalIgnoreCase);
        }
    }
}
