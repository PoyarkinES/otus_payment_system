using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Otus.PaymentSystem.Gateway.Host.Endpoints;

namespace Otus.PaymentSystem.Gateway.Host.Tests;

/// <summary>
/// Настройки, которые обязаны попасть в конфигурацию до старта Program:
/// JWKS указываем на заведомо закрытый порт, чтобы Gateway не ждал Identity 5 секунд.
/// </summary>
internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize() =>
        Environment.SetEnvironmentVariable("Jwt__JwksUri", "http://127.0.0.1:1/.well-known/jwks.json");
}

/// <summary>
/// HTTP-тесты BFF через реальный конвейер Gateway: маршрутизация, auth-политики,
/// rate limiter и ProblemDetails — то, что юнит-тесты хендлеров не проверяют.
/// </summary>
public sealed class BffEndpointsWebTests : IClassFixture<BffApiFactory>
{
    private readonly BffApiFactory _factory;

    public BffEndpointsWebTests(BffApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMe_Anonymous_ReturnsUnauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/bff/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetMe_Authenticated_ReturnsClaimsFromToken()
    {
        using HttpClient client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/me");
        request.Headers.Add("X-Test-Sub", BffApiFactory.UserId.ToString());
        request.Headers.Add("X-Test-Role", "Admin");

        using HttpResponseMessage response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"\"userId\":\"{BffApiFactory.UserId}\"", body);
        Assert.Contains("\"email\":\"test@example.com\"", body);
        Assert.Contains("\"role\":\"Admin\"", body);
    }

    [Fact]
    public async Task GetHealth_IdentityOk_ReturnsOk()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/bff/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"ok\"", body);
        Assert.Contains("\"identity\":\"ok\"", body);
    }
}

/// <summary>
/// Фабрика: подменяет auth на тестовую схему и HttpMessageHandler внутреннего клиента Identity.
/// </summary>
public sealed class BffApiFactory : WebApplicationFactory<Program>
{
    public static readonly Guid UserId = Guid.Parse("145b8d5c-2efa-4cc2-8f7b-470fb33b0227");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);

            services.AddHttpClient(BffEndpoints.IdentityClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new StubIdentityHandler());
        });
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var sub = Request.Headers["X-Test-Sub"].ToString();
            if (string.IsNullOrEmpty(sub))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim>
            {
                new("sub", sub),
                new("email", "test@example.com"),
                new("role", Request.Headers["X-Test-Role"].ToString())
            };

            var identity = new ClaimsIdentity(claims, "Test");
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }

    private sealed class StubIdentityHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
