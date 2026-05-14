using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Cryptography;
using System.Text;
using WebApplication1.Api.Middleware;
using WebApplication1.Infrastructure.Configuration;

namespace WebApplication1.Tests
{
    public class ValidateWhatsAppSignatureFilterTests
    {
        private readonly Mock<IOptions<WhatsAppSettings>> _mockSettings;
        private readonly Mock<ILogger<ValidateWhatsAppSignatureFilter>> _mockLogger;

        public ValidateWhatsAppSignatureFilterTests()
        {
            _mockSettings = new Mock<IOptions<WhatsAppSettings>>();
            _mockLogger = new Mock<ILogger<ValidateWhatsAppSignatureFilter>>();
        }

        private string ComputeHmacSha256(byte[] payload, string secret)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
            using var hmac = new HMACSHA256(keyBytes);
            byte[] hash = hmac.ComputeHash(payload);
            string hex = Convert.ToHexString(hash).ToLowerInvariant();
            return $"sha256={hex}";
        }

        [Fact]
        public async Task OnActionExecutionAsync_WithoutAppSecret_AllowsRequest()
        {
            // Arrange
            var settings = new WhatsAppSettings { AppSecret = "" };
            _mockSettings.Setup(x => x.Value).Returns(settings);

            var filter = new ValidateWhatsAppSignatureFilter(_mockSettings.Object, _mockLogger.Object);

            var actionContext = new ActionExecutingContext(
                new ActionContext(new DefaultHttpContext(), new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()),
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
        public async Task OnActionExecutionAsync_WithGetRequest_AllowsRequest()
        {
            // Arrange
            var settings = new WhatsAppSettings { AppSecret = "test_secret" };
            _mockSettings.Setup(x => x.Value).Returns(settings);

            var filter = new ValidateWhatsAppSignatureFilter(_mockSettings.Object, _mockLogger.Object);

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
        public async Task OnActionExecutionAsync_WithoutSignatureHeader_RejectRequest()
        {
            // Arrange
            var settings = new WhatsAppSettings { AppSecret = "test_secret" };
            _mockSettings.Setup(x => x.Value).Returns(settings);

            var filter = new ValidateWhatsAppSignatureFilter(_mockSettings.Object, _mockLogger.Object);

            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            // Sem header X-Hub-Signature-256

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
        public async Task OnActionExecutionAsync_WithValidSignature_AllowsRequest()
        {
            // Arrange
            var settings = new WhatsAppSettings { AppSecret = "test_secret" };
            _mockSettings.Setup(x => x.Value).Returns(settings);

            var filter = new ValidateWhatsAppSignatureFilter(_mockSettings.Object, _mockLogger.Object);

            var bodyContent = Encoding.UTF8.GetBytes("{\"test\": \"payload\"}");
            var validSignature = ComputeHmacSha256(bodyContent, "test_secret");

            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Headers["X-Hub-Signature-256"] = validSignature;
            context.Request.Body = new MemoryStream(bodyContent);
            context.Request.ContentLength = bodyContent.Length;

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
        public async Task OnActionExecutionAsync_WithInvalidSignature_RejectRequest()
        {
            // Arrange
            var settings = new WhatsAppSettings { AppSecret = "test_secret" };
            _mockSettings.Setup(x => x.Value).Returns(settings);

            var filter = new ValidateWhatsAppSignatureFilter(_mockSettings.Object, _mockLogger.Object);

            var bodyContent = Encoding.UTF8.GetBytes("{\"test\": \"payload\"}");
            var invalidSignature = "sha256=invalid_signature_here";

            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Headers["X-Hub-Signature-256"] = invalidSignature;
            context.Request.Body = new MemoryStream(bodyContent);
            context.Request.ContentLength = bodyContent.Length;

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
