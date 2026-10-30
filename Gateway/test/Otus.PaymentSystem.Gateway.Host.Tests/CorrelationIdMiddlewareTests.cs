using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Otus.PaymentSystem.Gateway.Host.Middleware;

namespace Otus.PaymentSystem.Gateway.Host.Tests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_PreservesIncomingCorrelationId()
    {
        const string correlationId = "test-correlation-id";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        Assert.Equal(
            correlationId,
            context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString());
        Assert.Equal(
            correlationId,
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationId_WhenMissing()
    {
        var context = new DefaultHttpContext();
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        var value = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.True(Guid.TryParseExact(value, "N", out _));
        Assert.Equal(
            value,
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationId_WhenIncomingValueIsInvalid()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "bad value!";
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        var value = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.True(Guid.TryParseExact(value, "N", out _));
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationId_WhenIncomingValueIsTooLong()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = new string('a', 129);
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        var value = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.True(Guid.TryParseExact(value, "N", out _));
    }

    private static CorrelationIdMiddleware CreateMiddleware() =>
        new(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance);
}
