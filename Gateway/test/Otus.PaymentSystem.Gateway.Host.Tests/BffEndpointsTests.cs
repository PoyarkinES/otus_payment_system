using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Otus.PaymentSystem.Gateway.Host.Endpoints;
using Otus.PaymentSystem.Gateway.Host.Middleware;

namespace Otus.PaymentSystem.Gateway.Host.Tests;

/// <summary>
/// Хендлеры BFF проверяются напрямую, без TestServer: JWKS, Serilog и YARP
/// к этой логике отношения не имеют, а auth-политики уже покрыты смоуком.
/// </summary>
public sealed class BffEndpointsTests
{
    private const string CorrelationId = "correlation-42";

    [Fact]
    public async Task GetMe_ValidClaims_ReturnsUserFromToken()
    {
        Guid userId = Guid.NewGuid();
        ClaimsPrincipal user = CreateUser(userId, "user@example.com", "Admin");

        var (status, body) = await ExecuteAsync(BffEndpoints.GetMe(user));

        Assert.Equal(StatusCodes.Status200OK, status);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal("user@example.com", json.RootElement.GetProperty("email").GetString());
        Assert.Equal("Admin", json.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public void GetMe_NoSub_ReturnsUnauthorized()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("email", "user@example.com"),
            new Claim("role", "User")
        }, "test"));

        AssertUnauthorized(BffEndpoints.GetMe(user));
    }

    [Fact]
    public void GetMe_SubIsNotGuid_ReturnsUnauthorized()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "not-a-guid"),
            new Claim("email", "user@example.com"),
            new Claim("role", "User")
        }, "test"));

        AssertUnauthorized(BffEndpoints.GetMe(user));
    }

    [Fact]
    public void GetMe_NoEmail_ReturnsUnauthorized()
    {
        ClaimsPrincipal user = CreateUser(Guid.NewGuid(), email: null, role: "User");

        AssertUnauthorized(BffEndpoints.GetMe(user));
    }

    [Fact]
    public void GetMe_NoRole_ReturnsUnauthorized()
    {
        ClaimsPrincipal user = CreateUser(Guid.NewGuid(), "user@example.com", role: null);

        AssertUnauthorized(BffEndpoints.GetMe(user));
    }

    [Fact]
    public async Task GetHealthAsync_IdentityOk_ReturnsOk()
    {
        var (status, body) = await RunHealthAsync(_ => new HttpResponseMessage(HttpStatusCode.OK));

        Assert.Equal(StatusCodes.Status200OK, status);
        AssertHealthBody(body, expectedStatus: "ok", expectedIdentity: "ok");
    }

    [Fact]
    public async Task GetHealthAsync_IdentityReturnsServerError_ReturnsServiceUnavailable()
    {
        var (status, body) = await RunHealthAsync(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        AssertHealthBody(body, expectedStatus: "degraded", expectedIdentity: "unavailable");
    }

    [Fact]
    public async Task GetHealthAsync_IdentityUnreachable_ReturnsServiceUnavailable()
    {
        var (status, body) = await RunHealthAsync(_ => throw new HttpRequestException("connection refused"));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        AssertHealthBody(body, expectedStatus: "degraded", expectedIdentity: "unavailable");
    }

    [Fact]
    public async Task GetHealthAsync_ForwardsCorrelationId()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await RunHealthAsync(handler);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            CorrelationId,
            handler.LastRequest!.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    private static Task<(int Status, string Body)> RunHealthAsync(
        Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        RunHealthAsync(new StubHandler(respond));

    private static async Task<(int Status, string Body)> RunHealthAsync(StubHandler handler)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = CorrelationId;

        IResult result = await BffEndpoints.GetHealthAsync(
            context,
            new StubHttpClientFactory(handler),
            NullLoggerFactory.Instance,
            CancellationToken.None);

        return await ExecuteAsync(result);
    }

    /// <summary>
    /// Исполнение результата: 200/503 пишут JSON и нуждаются в IOptions&lt;JsonOptions&gt;,
    /// поэтому контексту даётся минимальный провайдер сервисов.
    /// </summary>
    private static async Task<(int Status, string Body)> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddOptions().AddLogging().BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        return (context.Response.StatusCode, body);
    }

    /// <summary>
    /// 401 проверяем по типу результата: UnauthorizedHttpResult при исполнении запускает
    /// challenge через IAuthenticationService, то есть требует весь auth-конвейер.
    /// </summary>
    private static void AssertUnauthorized(IResult result)
    {
        var unauthorized = Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.Equal<int?>(
            StatusCodes.Status401Unauthorized,
            ((IStatusCodeHttpResult)unauthorized).StatusCode);
    }

    private static void AssertHealthBody(string body, string expectedStatus, string expectedIdentity)
    {
        using var json = JsonDocument.Parse(body);
        JsonElement root = json.RootElement;

        Assert.Equal(expectedStatus, root.GetProperty("status").GetString());
        Assert.Equal("ok", root.GetProperty("services").GetProperty("gateway").GetString());
        Assert.Equal(expectedIdentity, root.GetProperty("services").GetProperty("identity").GetString());
    }

    private static ClaimsPrincipal CreateUser(Guid userId, string? email, string? role)
    {
        var claims = new List<Claim> { new("sub", userId.ToString()) };

        if (email is not null)
            claims.Add(new Claim("email", email));

        if (role is not null)
            claims.Add(new Claim("role", role));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://identity.test/")
        };
    }
}
