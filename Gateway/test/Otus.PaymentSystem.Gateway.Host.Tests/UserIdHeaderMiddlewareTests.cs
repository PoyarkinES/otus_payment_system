using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Otus.PaymentSystem.Gateway.Host.Middleware;

namespace Otus.PaymentSystem.Gateway.Host.Tests;

public sealed class UserIdHeaderMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_Unauthenticated_RemovesSpoofedHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[UserIdHeaderMiddleware.HeaderName] = Guid.NewGuid().ToString();
        var nextCalled = false;
        var middleware = new UserIdHeaderMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.False(context.Request.Headers.ContainsKey(UserIdHeaderMiddleware.HeaderName));
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithSub_ReplacesSpoofedHeader()
    {
        var userId = Guid.NewGuid();
        var context = CreateAuthenticatedContext(userId);
        context.Request.Headers[UserIdHeaderMiddleware.HeaderName] = Guid.NewGuid().ToString();
        var middleware = new UserIdHeaderMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(
            userId.ToString(),
            context.Request.Headers[UserIdHeaderMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithInvalidSub_RemovesHeader()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim("sub", "not-a-guid") }, "test"))
        };
        context.Request.Headers[UserIdHeaderMiddleware.HeaderName] = Guid.NewGuid().ToString();
        var middleware = new UserIdHeaderMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.False(context.Request.Headers.ContainsKey(UserIdHeaderMiddleware.HeaderName));
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithoutSub_RemovesHeader()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim("email", "user@example.com") }, "test"))
        };
        context.Request.Headers[UserIdHeaderMiddleware.HeaderName] = Guid.NewGuid().ToString();
        var middleware = new UserIdHeaderMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.False(context.Request.Headers.ContainsKey(UserIdHeaderMiddleware.HeaderName));
    }

    private static DefaultHttpContext CreateAuthenticatedContext(Guid userId) =>
        new()
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) }, "test"))
        };
}
