using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Otus.PaymentSystem.AspNetCore.Middleware;
using Otus.PaymentSystem.AspNetCore.Problems;

namespace Otus.PaymentSystem.AspNetCore.Tests;

public sealed class UnhandledExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_Writes500WithoutExceptionDetails()
    {
        var context = NewContext();
        context.Request.Headers[CorrelationIdHeader.Name] = "corr-500";
        var middleware = CreateMiddleware(new ApiProblemOptions(), _ => throw new InvalidOperationException("boom"));

        await middleware.InvokeAsync(context);

        var json = await ReadBodyAsync(context);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal("Внутренняя ошибка сервера.", doc.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("boom", json);
        Assert.Equal("corr-500", doc.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task InvokeAsync_IncludesExceptionDetailsWhenEnabled()
    {
        var context = NewContext();
        var middleware = CreateMiddleware(
            new ApiProblemOptions { IncludeExceptionDetails = true },
            _ => throw new InvalidOperationException("boom"));

        await middleware.InvokeAsync(context);

        using var doc = JsonDocument.Parse(await ReadBodyAsync(context));
        Assert.Contains("boom", doc.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_RethrowsWhenResponseHasStarted()
    {
        var body = new MemoryStream();
        var context = NewStartedContext(body);
        var middleware = CreateMiddleware(new ApiProblemOptions(), _ => throw new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    private static UnhandledExceptionMiddleware CreateMiddleware(ApiProblemOptions options, RequestDelegate next) =>
        new(next, Options.Create(options), NullLogger<UnhandledExceptionMiddleware>.Instance);

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static DefaultHttpContext NewStartedContext(Stream body)
    {
        var features = new FeatureCollection();
        features.Set<IHttpResponseFeature>(new StartedHttpResponseFeature(body));
        return new DefaultHttpContext(features);
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class StartedHttpResponseFeature(Stream body) : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = body;
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}
