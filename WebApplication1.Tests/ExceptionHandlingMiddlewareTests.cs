using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Moq;
using WebApplication1.Api.Middleware;

namespace WebApplication1.Tests
{
    public class ExceptionHandlingMiddlewareTests
    {
        private class MockHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Development";
            public string ApplicationName { get; set; } = "TestApp";
            public string ContentRootPath { get; set; } = System.IO.Directory.GetCurrentDirectory();
            public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(System.IO.Directory.GetCurrentDirectory());
        }

        [Fact]
        public async Task InvokeAsync_WithValidRequest_CallsNextMiddleware()
        {
            // Arrange
            var mockNext = new Mock<RequestDelegate>();
            var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ExceptionHandlingMiddleware>>();
            var environment = new MockHostEnvironment { EnvironmentName = "Production" };

            mockNext.Setup(x => x(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

            var middleware = new ExceptionHandlingMiddleware(mockNext.Object, mockLogger.Object, environment);
            var context = new DefaultHttpContext();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            mockNext.Verify(x => x(context), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WithException_ReturnsBadRequestInDevelopment()
        {
            // Arrange
            var testException = new ArgumentNullException("testParam", "Test error message");

            var mockNext = new Mock<RequestDelegate>();
            mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .Throws(testException);

            var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ExceptionHandlingMiddleware>>();
            var environment = new MockHostEnvironment { EnvironmentName = "Development" };

            var middleware = new ExceptionHandlingMiddleware(mockNext.Object, mockLogger.Object, environment);
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(500, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);
            
            // In development, the response should contain the actual error message
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseContent = new StreamReader(context.Response.Body).ReadToEnd();
            Assert.Contains("Test error message", responseContent);
        }

        [Fact]
        public async Task InvokeAsync_WithException_ReturnsGenericMessageInProduction()
        {
            // Arrange
            var testException = new InvalidOperationException("Some error details");

            var mockNext = new Mock<RequestDelegate>();
            mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .Throws(testException);

            var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ExceptionHandlingMiddleware>>();
            var environment = new MockHostEnvironment { EnvironmentName = "Production" };

            var middleware = new ExceptionHandlingMiddleware(mockNext.Object, mockLogger.Object, environment);
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(500, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            // Verificar que a response contém apenas mensagem genérica, não o erro real
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            var responseBody = await reader.ReadToEndAsync();
            Assert.DoesNotContain("Some error details", responseBody);
        }

        [Fact]
        public async Task InvokeAsync_WithValidRequest_DoesNotChangeResponseStatusCode()
        {
            // Arrange
            var mockNext = new Mock<RequestDelegate>();
            mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .Callback<HttpContext>(ctx => ctx.Response.StatusCode = 200)
                .Returns(Task.CompletedTask);

            var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ExceptionHandlingMiddleware>>();
            var environment = new MockHostEnvironment { EnvironmentName = "Production" };

            var middleware = new ExceptionHandlingMiddleware(mockNext.Object, mockLogger.Object, environment);
            var context = new DefaultHttpContext();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(200, context.Response.StatusCode);
        }
    }
}

