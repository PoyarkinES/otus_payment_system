using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Host.Tests;

/// <summary>
/// Конфигурация, которая обязана быть до старта Program:
/// в не-Development нужен ключ RS256, а строка подключения — только чтобы DI не упал.
/// </summary>
internal static class IdentityTestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        using var rsa = RSA.Create(2048);
        Environment.SetEnvironmentVariable("Jwt__PrivateKeyPem", rsa.ExportRSAPrivateKeyPem());
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__IdentityDb",
            "Host=127.0.0.1;Port=1;Database=identity_tests;Username=tests;Password=tests");
    }
}

/// <summary>
/// HTTP-тесты Identity через реальный конвейер: 401 без токена, 403 для не-админа,
/// 200 для админа. Репозиторий подменён фейком — БД в тестах не нужна.
/// </summary>
public sealed class UsersEndpointsWebTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public UsersEndpointsWebTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetUserById_Anonymous_ReturnsUnauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_RegularUser_ReturnsForbidden()
    {
        using HttpClient client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/users/{Guid.NewGuid()}");
        request.Headers.Add("X-Test-Sub", Guid.NewGuid().ToString());
        request.Headers.Add("X-Test-Role", "User");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_Admin_ReturnsProfileWithStringStatus()
    {
        using HttpClient client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/users/{Guid.NewGuid()}");
        request.Headers.Add("X-Test-Sub", Guid.NewGuid().ToString());
        request.Headers.Add("X-Test-Role", "Admin");

        using HttpResponseMessage response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"email\":\"test@example.com\"", body);
        Assert.Contains("\"status\":\"Active\"", body);
    }
}

/// <summary>Фабрика: тестовая auth-схема и фейковый репозиторий вместо EF.</summary>
public sealed class IdentityApiFactory : WebApplicationFactory<global::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Не Development: иначе подтянутся user-secrets и сидер полезет в БД.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.AddScoped<IUserRepository>(_ => new FakeUserRepository());

            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);
        });
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private static readonly User Stored = User.Register(new Email("test@example.com"), "hash");

        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
            Task.FromResult<User?>(Stored);

        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<User?>(Stored);

        public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
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

            var claims = new List<Claim> { new("sub", sub), new("email", "test@example.com") };

            var role = Request.Headers["X-Test-Role"].ToString();
            if (!string.IsNullOrEmpty(role))
                claims.Add(new Claim("role", role));

            var identity = new ClaimsIdentity(claims, "Test");
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }
}
