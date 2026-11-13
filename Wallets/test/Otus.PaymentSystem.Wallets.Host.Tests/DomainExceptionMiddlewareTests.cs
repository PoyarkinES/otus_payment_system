using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Host.Middleware;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Otus.PaymentSystem.Wallets.Host.Tests;

public class DomainExceptionMiddlewareTests
{
    [Theory]
    [InlineData(DomainError.Validation, 400)]
    [InlineData(DomainError.Unauthorized, 401)]
    [InlineData(DomainError.Forbidden, 403)]
    [InlineData(DomainError.NotFound, 404)]
    [InlineData(DomainError.Conflict, 409)]
    public async Task InvokeAsync_DomainException_MapsByErrorCode(DomainError error, int expectedStatus)
    {
        var (status, _, _) = await InvokeAsync(new DomainException("сообщение", error));

        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public async Task InvokeAsync_DomainException_ReturnsProblemDetailsWithTraceId()
    {
        var (_, body, contentType) = await InvokeAsync(
            new DomainException("Кошелёк не найден.", DomainError.NotFound),
            correlationId: "corr-404");

        Assert.StartsWith("application/problem+json", contentType);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(404, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Кошелёк не найден.", doc.RootElement.GetProperty("detail").GetString());
        Assert.Equal("corr-404", doc.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task InvokeAsync_NonDomainException_Rethrows()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        RequestDelegate next = _ => throw new InvalidOperationException("boom");
        var middleware = new DomainExceptionMiddleware(next, NullLogger<DomainExceptionMiddleware>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    private static async Task<(int StatusCode, string Body, string ContentType)> InvokeAsync(
        Exception toThrow,
        string? correlationId = null)
    {
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;
        if (correlationId is not null)
            context.Request.Headers["X-Correlation-Id"] = correlationId;

        RequestDelegate next = _ => throw toThrow;
        var middleware = new DomainExceptionMiddleware(next, NullLogger<DomainExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        body.Position = 0;
        var text = await new StreamReader(body).ReadToEndAsync();
        return (context.Response.StatusCode, text, context.Response.ContentType ?? string.Empty);
    }
}
