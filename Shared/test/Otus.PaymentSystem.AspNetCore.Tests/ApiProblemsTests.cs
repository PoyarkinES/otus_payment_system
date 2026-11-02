using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Otus.PaymentSystem.AspNetCore.Problems;

namespace Otus.PaymentSystem.AspNetCore.Tests;

public sealed class ApiProblemsTests
{
    [Fact]
    public void Create_FillsTypeTitleAndStatus()
    {
        var problem = ApiProblems.Create(StatusCodes.Status404NotFound, "Не найдено.");

        Assert.Equal(404, problem.Status);
        Assert.Equal("Not Found", problem.Title);
        Assert.Equal("https://httpstatuses.io/404", problem.Type);
        Assert.Equal("Не найдено.", problem.Detail);
    }

    [Fact]
    public async Task WriteAsync_SetsContentTypeAndTraceId()
    {
        var context = NewContext();
        context.Request.Headers[CorrelationIdHeader.Name] = "corr-1";

        await ApiProblems.WriteAsync(context, StatusCodes.Status401Unauthorized, "Нет доступа.");

        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal(ApiProblems.ContentType, context.Response.ContentType);

        using var doc = JsonDocument.Parse(await ReadBodyAsync(context));
        Assert.Equal("corr-1", doc.RootElement.GetProperty("traceId").GetString());
        Assert.Equal(401, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Unauthorized", doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ValidationResult_SerializesErrorsWithCamelCaseKeys()
    {
        var context = NewContext();
        var result = ApiProblems.ValidationResult(new Dictionary<string, string[]>
        {
            ["Email"] = ["Некорректный формат email."]
        });

        await result.ExecuteAsync(context);

        using var doc = JsonDocument.Parse(await ReadBodyAsync(context));
        var error = Assert.Single(doc.RootElement.GetProperty("errors").EnumerateObject());
        Assert.Equal("email", error.Name);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task WriteAsync_DoesNotTouchStartedResponse()
    {
        var body = new MemoryStream();
        var context = NewStartedContext(body);

        await ApiProblems.WriteAsync(context, StatusCodes.Status500InternalServerError);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(0, body.Length);
    }

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
