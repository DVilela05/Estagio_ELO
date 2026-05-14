using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WebApplication1.Api.Middleware;
using WebApplication1.Infrastructure.Configuration;

namespace WebApplication1.Tests
{
    public class ValidateTeamsJwtFilterTests
    {
        private class MockHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Development";
            public string ApplicationName { get; set; } = "TestApp";
            public string ContentRootPath { get; set; } = System.IO.Directory.GetCurrentDirectory();
            public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(System.IO.Directory.GetCurrentDirectory());
        }

        private readonly Mock<IOptions<TeamsSettings>> _mockSettings;
        private readonly Mock<ILogger<ValidateTeamsJwtFilter>> _mockLogger;

        public ValidateTeamsJwtFilterTests()
        {
            _mockSettings = new Mock<IOptions<TeamsSettings>>();
            _mockLogger = new Mock<ILogger<ValidateTeamsJwtFilter>>();
        }

        [Fact]
        public async Task OnActionExecutionAsync_InDevelopmentEnvironment_SkipsValidation()
        {
            // Arrange
            var settings = new TeamsSettings { BotId = "test-bot-id" };
            _mockSettings.Setup(x => x.Value).Returns(settings);
            var environment = new MockHostEnvironment { EnvironmentName = "Development" };

            var filter = new ValidateTeamsJwtFilter(_mockSettings.Object, environment, _mockLogger.Object);

            var context = new DefaultHttpContext();
            context.Request.Method = "POST";

            var actionContext = new ActionExecutingContext(
                new ActionContext(context, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()),
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                new object());

            var nextCalled = false;
            ActionExecutionDelegate next = async () =>
            {
                nextCalled = true;
                return new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object());
            };

            // Act
            await filter.OnActionExecutionAsync(actionContext, next);

            // Assert
            Assert.True(nextCalled);
            Assert.Null(actionContext.Result);
        }

        [Fact]
        public async Task OnActionExecutionAsync_InProductionWithoutBotId_ReturnsError()
        {
            // Arrange
            var settings = new TeamsSettings { BotId = "" };
            _mockSettings.Setup(x => x.Value).Returns(settings);
            var environment = new MockHostEnvironment { EnvironmentName = "Production" };

            var filter = new ValidateTeamsJwtFilter(_mockSettings.Object, environment, _mockLogger.Object);

            var context = new DefaultHttpContext();
            context.Request.Method = "POST";

            var actionContext = new ActionExecutingContext(
                new ActionContext(context, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()),
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                new object());

            ActionExecutionDelegate next = async () =>
            {
                return new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object());
            };

            // Act
            await filter.OnActionExecutionAsync(actionContext, next);

            // Assert
            Assert.NotNull(actionContext.Result);
            Assert.IsType<StatusCodeResult>(actionContext.Result);
            var statusResult = actionContext.Result as StatusCodeResult;
            Assert.Equal(StatusCodes.Status500InternalServerError, statusResult?.StatusCode);
        }

        [Fact]
        public async Task OnActionExecutionAsync_WithGetRequest_SkipsValidation()
        {
            // Arrange
            var settings = new TeamsSettings { BotId = "test-bot-id" };
            _mockSettings.Setup(x => x.Value).Returns(settings);
            var environment = new MockHostEnvironment { EnvironmentName = "Production" };

            var filter = new ValidateTeamsJwtFilter(_mockSettings.Object, environment, _mockLogger.Object);

            var context = new DefaultHttpContext();
            context.Request.Method = "GET";

            var actionContext = new ActionExecutingContext(
                new ActionContext(context, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()),
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                new object());

            var nextCalled = false;
            ActionExecutionDelegate next = async () =>
            {
                nextCalled = true;
                return new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object());
            };

            // Act
            await filter.OnActionExecutionAsync(actionContext, next);

            // Assert
            Assert.True(nextCalled);
        }

        [Fact]
        public async Task OnActionExecutionAsync_WithoutAuthorizationHeader_RejectRequest()
        {
            // Arrange
            var settings = new TeamsSettings { BotId = "test-bot-id" };
            _mockSettings.Setup(x => x.Value).Returns(settings);
            var environment = new MockHostEnvironment { EnvironmentName = "Production" };

            var filter = new ValidateTeamsJwtFilter(_mockSettings.Object, environment, _mockLogger.Object);

            var context = new DefaultHttpContext();
            context.Request.Method = "POST";

            var actionContext = new ActionExecutingContext(
                new ActionContext(context, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()),
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                new object());

            ActionExecutionDelegate next = async () =>
            {
                return new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object());
            };

            // Act
            await filter.OnActionExecutionAsync(actionContext, next);

            // Assert
            Assert.NotNull(actionContext.Result);
            Assert.IsType<UnauthorizedObjectResult>(actionContext.Result);
        }

        [Fact]
        public async Task OnActionExecutionAsync_WithInvalidBearerFormat_RejectRequest()
        {
            // Arrange
            var settings = new TeamsSettings { BotId = "test-bot-id" };
            _mockSettings.Setup(x => x.Value).Returns(settings);
            var environment = new MockHostEnvironment { EnvironmentName = "Production" };

            var filter = new ValidateTeamsJwtFilter(_mockSettings.Object, environment, _mockLogger.Object);

            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Headers["Authorization"] = "InvalidFormat";

            var actionContext = new ActionExecutingContext(
                new ActionContext(context, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()),
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                new object());

            ActionExecutionDelegate next = async () =>
            {
                return new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object());
            };

            // Act
            await filter.OnActionExecutionAsync(actionContext, next);

            // Assert
            Assert.NotNull(actionContext.Result);
            Assert.IsType<UnauthorizedObjectResult>(actionContext.Result);
        }

        [Fact]
        public async Task OnActionExecutionAsync_WithEmptyToken_RejectRequest()
        {
            // Arrange
            var settings = new TeamsSettings { BotId = "test-bot-id" };
            _mockSettings.Setup(x => x.Value).Returns(settings);
            var environment = new MockHostEnvironment { EnvironmentName = "Production" };

            var filter = new ValidateTeamsJwtFilter(_mockSettings.Object, environment, _mockLogger.Object);

            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Headers["Authorization"] = "Bearer ";

            var actionContext = new ActionExecutingContext(
                new ActionContext(context, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()),
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                new object());

            ActionExecutionDelegate next = async () =>
            {
                return new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object());
            };

            // Act
            await filter.OnActionExecutionAsync(actionContext, next);

            // Assert
            Assert.NotNull(actionContext.Result);
            Assert.IsType<UnauthorizedObjectResult>(actionContext.Result);
        }

        [Fact]
        public async Task OnActionExecutionAsync_WithInvalidToken_RejectRequest()
        {
            // Arrange
            var settings = new TeamsSettings { BotId = "test-bot-id" };
            _mockSettings.Setup(x => x.Value).Returns(settings);
            var environment = new MockHostEnvironment { EnvironmentName = "Production" };

            var filter = new ValidateTeamsJwtFilter(_mockSettings.Object, environment, _mockLogger.Object);

            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Headers["Authorization"] = "Bearer invalid.token.here";

            var actionContext = new ActionExecutingContext(
                new ActionContext(context, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()),
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                new object());

            ActionExecutionDelegate next = async () =>
            {
                return new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object());
            };

            // Act
            await filter.OnActionExecutionAsync(actionContext, next);

            // Assert
            Assert.NotNull(actionContext.Result);
            Assert.IsType<UnauthorizedObjectResult>(actionContext.Result);
        }
    }
}
