using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Otus.PaymentSystem.AspNetCore.Middleware;

namespace Otus.PaymentSystem.AspNetCore.Tests;

public sealed class EmptyResponseProblemMiddlewareTests
{
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(502)]
    [InlineData(504)]
    public async Task InvokeAsync_FillsEmptyErrorResponse(int status)
    {
        var context = NewContext();
        var middleware = new EmptyResponseProblemMiddleware(ctx =>
        {
            ctx.Response.StatusCode = status;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("application/problem+json; charset=utf-8", context.Response.ContentType);

        using var doc = JsonDocument.Parse(await ReadBodyAsync(context));
        Assert.Equal(status, doc.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task InvokeAsync_DoesNotTouchResponseWithBody()
    {
        var context = NewContext();
        var middleware = new EmptyResponseProblemMiddleware(async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsync("{\"status\":400}");
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("{\"status\":400}", await ReadBodyAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_IgnoresSuccessResponses()
    {
        var context = NewContext();
        var middleware = new EmptyResponseProblemMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(204, context.Response.StatusCode);
        Assert.Null(context.Response.ContentType);
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
