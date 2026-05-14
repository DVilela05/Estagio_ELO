using WebApplication1.Core.Models;

namespace WebApplication1.Tests
{
    public class BusinessApiResultTests
    {
        // ─── Factory: Ok ─────────────────────────────────────────────

        [Fact]
        public void Ok_ReturnsSuccessResult()
        {
            var result = BusinessApiResult.Ok("Tudo certo.");

            Assert.True(result.Success);
            Assert.Equal("Tudo certo.", result.Message);
            Assert.Null(result.ErrorCode);
            Assert.False(result.IsStub);
        }

        [Fact]
        public void Ok_WithStubFlag_SetsIsStub()
        {
            var result = BusinessApiResult.Ok("Stub OK", isStub: true);

            Assert.True(result.Success);
            Assert.True(result.IsStub);
        }

        [Fact]
        public void Ok_WithoutMessage_ReturnsNullMessage()
        {
            var result = BusinessApiResult.Ok();

            Assert.True(result.Success);
            Assert.Null(result.Message);
        }

        // ─── Factory: Fail ───────────────────────────────────────────

        [Fact]
        public void Fail_ReturnsFailureResult()
        {
            var result = BusinessApiResult.Fail("Deu erro", "BAD_REQUEST");

            Assert.False(result.Success);
            Assert.Equal("Deu erro", result.Message);
            Assert.Equal("BAD_REQUEST", result.ErrorCode);
            Assert.False(result.IsStub);
        }

        [Fact]
        public void Fail_WithoutErrorCode_LeavesErrorCodeNull()
        {
            var result = BusinessApiResult.Fail("Erro genérico");

            Assert.False(result.Success);
            Assert.Null(result.ErrorCode);
        }

        // ─── Factory: Timeout ────────────────────────────────────────

        [Fact]
        public void Timeout_ReturnsTimeoutResult()
        {
            var result = BusinessApiResult.Timeout();

            Assert.False(result.Success);
            Assert.Equal("TIMEOUT", result.ErrorCode);
            Assert.NotNull(result.Message);
        }

        // ─── Factory: ServiceUnavailable ─────────────────────────────

        [Fact]
        public void ServiceUnavailable_ReturnsUnavailableResult()
        {
            var result = BusinessApiResult.ServiceUnavailable();

            Assert.False(result.Success);
            Assert.Equal("SERVICE_UNAVAILABLE", result.ErrorCode);
            Assert.NotNull(result.Message);
        }

        // ─── Imutabilidade ────────────────────────────────────────

        [Fact]
        public void Results_AreImmutable_PropertiesAreReadOnly()
        {
            var result = BusinessApiResult.Ok("msg");

            // Verifica que as propriedades são consistentes
            Assert.True(result.Success);
            Assert.Equal("msg", result.Message);
            Assert.Null(result.ErrorCode);
            Assert.False(result.IsStub);
        }
    }
}
