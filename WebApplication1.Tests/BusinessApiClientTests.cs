using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.Configuration;
using WebApplication1.Infrastructure.ExternalApis;

namespace WebApplication1.Tests
{
    public class BusinessApiClientTests
    {
        private sealed class CaptureHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }
            public string? LastBody { get; private set; }
            public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
            public string ResponseBody { get; set; } = "{\"success\":true,\"message\":\"ok\"}";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(StatusCode)
                {
                    Content = new StringContent(ResponseBody)
                };
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────

        private static BusinessApiClient CreateStubClient()
        {
            var settings = new BusinessApiSettings { BaseUrl = "" }; // stub mode
            var options = Options.Create(settings);
            var logger = new Mock<ILogger<BusinessApiClient>>();
            var httpClient = new HttpClient();

            return new BusinessApiClient(httpClient, options, logger.Object);
        }

        private static BusinessApiClient CreateRealClient(HttpClient httpClient, string baseUrl = "http://localhost:5000")
        {
            var settings = new BusinessApiSettings
            {
                BaseUrl = baseUrl,
                AttendancePath = "/api/attendance",
                ServiceToken = "service-token-test",
                HmacSecret = "hmac-secret-test",
                AllowInsecureHttp = true
            };
            var options = Options.Create(settings);
            var logger = new Mock<ILogger<BusinessApiClient>>();

            return new BusinessApiClient(httpClient, options, logger.Object);
        }

        // ─── BusinessApiSettings ─────────────────────────────────────

        [Theory]
        [InlineData("", true)]
        [InlineData(null, true)]
        [InlineData("   ", true)]
        [InlineData("http://localhost:5000", false)]
        [InlineData("https://api.empresa.pt", false)]
        public void Settings_IsStubMode_DependsOnBaseUrl(string? baseUrl, bool expectedStub)
        {
            var settings = new BusinessApiSettings { BaseUrl = baseUrl! };
            Assert.Equal(expectedStub, settings.IsStubMode);
        }

        [Fact]
        public void Settings_DefaultValues_AreReasonable()
        {
            var settings = new BusinessApiSettings();

            Assert.Equal(30, settings.TimeoutSeconds);
            Assert.Equal(3, settings.MaxRetries);
            Assert.True(settings.IsStubMode); // sem BaseUrl = stub
        }

        // ─── Stub Mode: RegisterAttendanceAsync ──────────────────────

        [Fact]
        public async Task RegisterAttendance_StubMode_ReturnsOkWithStubFlag()
        {
            var client = CreateStubClient();

            var result = await client.RegisterAttendanceAsync(
                "user1", "Diogo", "presente", "WhatsApp");

            Assert.True(result.Success);
            Assert.True(result.IsStub);
        }

        [Fact]
        public async Task RegisterAttendance_StubMode_WorksWithNullUserName()
        {
            var client = CreateStubClient();

            var result = await client.RegisterAttendanceAsync(
                "user1", null, "presente", "Teams");

            Assert.True(result.Success);
            Assert.True(result.IsStub);
        }

        // ─── Stub Mode: GetUserInfoAsync ─────────────────────────────

        [Fact]
        public async Task GetUserInfo_StubMode_ReturnsOkWithStubFlag()
        {
            var client = CreateStubClient();

            var result = await client.GetUserInfoAsync("user1");

            Assert.True(result.Success);
            Assert.True(result.IsStub);
        }

        // ─── Stub Mode: IsAvailableAsync ─────────────────────────────

        [Fact]
        public async Task IsAvailable_StubMode_ReturnsTrue()
        {
            var client = CreateStubClient();

            var result = await client.IsAvailableAsync();

            Assert.True(result);
        }

        // ─── Real Mode: connection errors ────────────────────────────

        [Fact]
        public async Task RegisterAttendance_RealMode_WhenServerDown_ReturnsServiceUnavailable()
        {
            // Arrange — HttpClient que aponta para um servidor que não existe
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:1/") // porta inválida
            };
            var client = CreateRealClient(httpClient, "http://localhost:1");

            // Act
            var result = await client.RegisterAttendanceAsync(
                "user1", "Diogo", "presente", "WhatsApp");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("SERVICE_UNAVAILABLE", result.ErrorCode);
        }

        [Fact]
        public async Task RegisterAttendance_RealMode_SendsSignedRequestHeaders()
        {
            var handler = new CaptureHandler();
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5008/")
            };

            var client = CreateRealClient(httpClient, "http://localhost:5008");

            var result = await client.RegisterAttendanceAsync(
                "user1", "Diogo", "presente", "WhatsApp", userPhone: "+351900000000");

            Assert.True(result.Success);
            Assert.NotNull(handler.LastRequest);
            Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization?.Scheme);
            Assert.Equal("service-token-test", handler.LastRequest.Headers.Authorization?.Parameter);
            Assert.True(handler.LastRequest.Headers.Contains("X-Request-Timestamp"));
            Assert.True(handler.LastRequest.Headers.Contains("X-Request-Nonce"));
            Assert.True(handler.LastRequest.Headers.Contains("X-Request-Signature"));
            Assert.True(handler.LastRequest.Headers.Contains("X-Signature-Version"));
            Assert.NotNull(handler.LastBody);
            using var doc = JsonDocument.Parse(handler.LastBody!);
            var root = doc.RootElement;
            Assert.Equal("user1", root.GetProperty("userId").GetString());
            Assert.Equal("900000000", root.GetProperty("whatsappPhone").GetString());
        }

        [Fact]
        public async Task RegisterAttendance_RealMode_UsesUserIdAsWhatsAppPhone_AndRemoves351Prefix()
        {
            var handler = new CaptureHandler();
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5008/")
            };

            var client = CreateRealClient(httpClient, "http://localhost:5008");

            var result = await client.RegisterAttendanceAsync(
                "351932947533", "Diogo", "presente", "WhatsApp", userPhone: null);

            Assert.True(result.Success);
            Assert.NotNull(handler.LastBody);

            using var doc = JsonDocument.Parse(handler.LastBody!);
            var root = doc.RootElement;
            Assert.Equal("932947533", root.GetProperty("whatsappPhone").GetString());
        }

        [Fact]
        public async Task RegisterAttendance_RealMode_WithoutSecurityConfig_ReturnsSecurityConfigError()
        {
            var httpClient = new HttpClient(new CaptureHandler())
            {
                BaseAddress = new Uri("http://localhost:5008/")
            };

            var settings = new BusinessApiSettings
            {
                BaseUrl = "http://localhost:5008",
                AllowInsecureHttp = true,
                AttendancePath = "/api/attendance"
            };

            var options = Options.Create(settings);
            var logger = new Mock<ILogger<BusinessApiClient>>();
            var client = new BusinessApiClient(httpClient, options, logger.Object);

            var result = await client.RegisterAttendanceAsync("user1", "Diogo", "presente", "WhatsApp");

            Assert.False(result.Success);
            Assert.Equal("SECURITY_CONFIG_INVALID", result.ErrorCode);
        }

        [Fact]
        public async Task IsAvailable_RealMode_WhenServerDown_ReturnsFalse()
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:1/")
            };
            var client = CreateRealClient(httpClient, "http://localhost:1");

            var result = await client.IsAvailableAsync();

            Assert.False(result);
        }
    }
}
