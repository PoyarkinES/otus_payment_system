using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Host.Middleware;

namespace Otus.PaymentSystem.Identity.Host.Tests;

public class DomainExceptionMiddlewareTests
{
    private static async Task<(int StatusCode, string Body, string ContentType)> InvokeAsync(Exception toThrow)
    {
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        RequestDelegate next = _ => throw toThrow;
        var middleware = new DomainExceptionMiddleware(next);

        await middleware.InvokeAsync(context);

        body.Position = 0;
        var text = await new StreamReader(body).ReadToEndAsync();
        return (context.Response.StatusCode, text, context.Response.ContentType ?? string.Empty);
    }

    [Theory]
    [InlineData("Неверные учётные данные.", 401)]
    [InlineData("Пользователь заблокирован.", 403)]
    [InlineData("Email уже занят.", 409)]
    [InlineData("Что-то ещё из домена.", 400)]
    public async Task InvokeAsync_DomainException_MapsToExpectedStatusCode(string message, int expectedStatus)
    {
        var (status, _, _) = await InvokeAsync(new DomainException(message));
        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public async Task InvokeAsync_DomainException_ReturnsProblemDetailsJson()
    {
        var (_, body, contentType) = await InvokeAsync(new DomainException("Неверные учётные данные."));

        Assert.StartsWith("application/problem+json", contentType);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(problem);
        Assert.Equal(401, problem!.Status);
        Assert.Equal("Неверные учётные данные.", problem.Detail);
    }

    [Fact]
    public async Task InvokeAsync_NonDomainException_Rethrows()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        RequestDelegate next = _ => throw new InvalidOperationException("boom");
        var middleware = new DomainExceptionMiddleware(next);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }
}
