using Microsoft.Extensions.Caching.Memory;
using WebApplication1.Application;

namespace WebApplication1.Tests
{
    public class WebhookConcurrencyGuardTests
    {
        private static WebhookConcurrencyGuard CreateGuard()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            return new WebhookConcurrencyGuard(cache);
        }

        [Fact]
        public void TryRegisterMessageId_NewId_ReturnsTrue()
        {
            var guard = CreateGuard();

            var first = guard.TryRegisterMessageId("wamid.1");

            Assert.True(first);
        }

        [Fact]
        public void TryRegisterMessageId_DuplicateId_ReturnsFalse()
        {
            var guard = CreateGuard();

            var first = guard.TryRegisterMessageId("wamid.dup");
            var second = guard.TryRegisterMessageId("wamid.dup");

            Assert.True(first);
            Assert.False(second);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void TryRegisterMessageId_InvalidValue_ReturnsFalse(string id)
        {
            var guard = CreateGuard();

            var result = guard.TryRegisterMessageId(id);

            Assert.False(result);
        }

        [Fact]
        public void TryAcquireSenderLock_FirstAcquire_ReturnsTrue()
        {
            var guard = CreateGuard();

            var acquired = guard.TryAcquireSenderLock("351900000001");

            Assert.True(acquired);
        }

        [Fact]
        public void TryAcquireSenderLock_SecondAcquireWithoutRelease_ReturnsFalse()
        {
            var guard = CreateGuard();

            var first = guard.TryAcquireSenderLock("351900000002");
            var second = guard.TryAcquireSenderLock("351900000002");

            Assert.True(first);
            Assert.False(second);
        }

        [Fact]
        public void ReleaseSenderLock_AfterRelease_CanAcquireAgain()
        {
            var guard = CreateGuard();
            var sender = "351900000003";

            var first = guard.TryAcquireSenderLock(sender);
            guard.ReleaseSenderLock(sender);
            var second = guard.TryAcquireSenderLock(sender);

            Assert.True(first);
            Assert.True(second);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void TryAcquireSenderLock_InvalidValue_ReturnsFalse(string sender)
        {
            var guard = CreateGuard();

            var result = guard.TryAcquireSenderLock(sender);

            Assert.False(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ReleaseSenderLock_InvalidValue_DoesNotThrow(string sender)
        {
            var guard = CreateGuard();

            var exception = Record.Exception(() => guard.ReleaseSenderLock(sender));

            Assert.Null(exception);
        }
    }
}
