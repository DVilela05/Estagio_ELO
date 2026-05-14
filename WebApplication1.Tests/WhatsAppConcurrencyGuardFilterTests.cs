using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using WebApplication1.Api.Middleware;
using WebApplication1.Application;

namespace WebApplication1.Tests
{
    public class WhatsAppConcurrencyGuardFilterTests
    {
        private static ActionExecutingContext CreateContext(DefaultHttpContext httpContext)
        {
            var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
            return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        }

        private static ActionExecutionDelegate CreateNext(ActionExecutingContext context, Action onCalled)
        {
            return async () =>
            {
                onCalled();
                return await Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
            };
        }

        private static WhatsAppConcurrencyGuardFilter CreateFilter()
        {
            var guard = new WebhookConcurrencyGuard(new MemoryCache(new MemoryCacheOptions()));
            var logger = new Mock<ILogger<WhatsAppConcurrencyGuardFilter>>();
            return new WhatsAppConcurrencyGuardFilter(guard, logger.Object);
        }

        private static string CreatePayload(params (string id, string from)[] messages)
        {
            var messagesJson = string.Join(",", messages.Select(m =>
                $"{{\"id\":\"{m.id}\",\"from\":\"{m.from}\",\"text\":{{\"body\":\"presente\"}}}}"));

            return
                "{" +
                "\"entry\":[{" +
                "\"changes\":[{" +
                "\"value\":{" +
                $"\"messages\":[{messagesJson}]" +
                "}}]}]}";
        }

        [Fact]
        public async Task OnActionExecutionAsync_NonPost_CallsNext()
        {
            var filter = CreateFilter();
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Get;

            var actionContext = CreateContext(context);
            var nextCalled = false;

            await filter.OnActionExecutionAsync(actionContext, CreateNext(actionContext, () => nextCalled = true));

            Assert.True(nextCalled);
            Assert.Null(actionContext.Result);
        }

        [Fact]
        public async Task OnActionExecutionAsync_PostValidPayload_StoresAcceptedIds_AndCallsNext()
        {
            var filter = CreateFilter();
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Post;

            var payload = CreatePayload(("wamid.100", "351911111111"));
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            context.Request.ContentLength = context.Request.Body.Length;

            var actionContext = CreateContext(context);
            var nextCalled = false;

            await filter.OnActionExecutionAsync(actionContext, CreateNext(actionContext, () => nextCalled = true));

            Assert.True(nextCalled);
            Assert.True(actionContext.HttpContext.Items.ContainsKey(WhatsAppConcurrencyGuardFilter.AcceptedMessageIdsItemKey));

            var ids = Assert.IsType<HashSet<string>>(actionContext.HttpContext.Items[WhatsAppConcurrencyGuardFilter.AcceptedMessageIdsItemKey]);
            Assert.Contains("wamid.100", ids);
        }

        [Fact]
        public async Task OnActionExecutionAsync_PostInvalidJson_ReturnsOkFastWithoutCallingNext()
        {
            var filter = CreateFilter();
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Post;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{ invalid json"));
            context.Request.ContentLength = context.Request.Body.Length;

            var actionContext = CreateContext(context);
            var nextCalled = false;

            await filter.OnActionExecutionAsync(actionContext, CreateNext(actionContext, () => nextCalled = true));

            Assert.False(nextCalled);
            Assert.IsType<OkResult>(actionContext.Result);
        }

        [Fact]
        public async Task OnActionExecutionAsync_PostTwoMessagesSameSender_AcceptsOnlyFirst()
        {
            var filter = CreateFilter();
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Post;

            var payload = CreatePayload(
                ("wamid.200", "351922222222"),
                ("wamid.201", "351922222222"));

            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            context.Request.ContentLength = context.Request.Body.Length;

            var actionContext = CreateContext(context);
            var nextCalled = false;

            await filter.OnActionExecutionAsync(actionContext, CreateNext(actionContext, () => nextCalled = true));

            Assert.True(nextCalled);
            var ids = Assert.IsType<HashSet<string>>(actionContext.HttpContext.Items[WhatsAppConcurrencyGuardFilter.AcceptedMessageIdsItemKey]);
            Assert.Single(ids);
            Assert.Contains("wamid.200", ids);
            Assert.DoesNotContain("wamid.201", ids);
        }

        [Fact]
        public async Task OnActionExecutionAsync_PostWithoutMessages_ReturnsOkFast()
        {
            var filter = CreateFilter();
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Post;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"entry\":[]}"));
            context.Request.ContentLength = context.Request.Body.Length;

            var actionContext = CreateContext(context);
            var nextCalled = false;

            await filter.OnActionExecutionAsync(actionContext, CreateNext(actionContext, () => nextCalled = true));

            Assert.False(nextCalled);
            Assert.IsType<OkResult>(actionContext.Result);
        }
    }
}
