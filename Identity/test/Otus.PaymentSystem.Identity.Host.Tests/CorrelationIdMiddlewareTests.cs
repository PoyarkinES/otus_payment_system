using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Otus.PaymentSystem.Identity.Host.Middleware;

namespace Otus.PaymentSystem.Identity.Host.Tests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ValidHeader_IsPreservedAndReturned()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "test-123";

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("test-123", context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_InvalidHeader_IsReplacedWithGuid()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "bad id!";

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var value = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();

        Assert.NotEqual("bad id!", value);
        Assert.True(Guid.TryParseExact(value, "N", out _));
    }
}
