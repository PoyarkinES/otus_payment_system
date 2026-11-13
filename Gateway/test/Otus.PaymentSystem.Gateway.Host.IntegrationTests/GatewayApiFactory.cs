extern alias WalletsHost;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using WalletsProgram = WalletsHost::Program;

namespace Otus.PaymentSystem.Gateway.Host.IntegrationTests;

public sealed class GatewayApiFactory : IAsyncLifetime
{
    private readonly string _connectionString;

    public GatewayApiFactory()
    {
        _connectionString = Environment.GetEnvironmentVariable("WALLETS_API_TEST_DB")
            ?? throw new InvalidOperationException(
                "Задайте WALLETS_API_TEST_DB (Host=localhost;Port=5433;Database=otus_wallets_api_test;Username=otus;Password=payments).");
    }

    public WebApplicationFactory<WalletsProgram> Wallets { get; private set; } = null!;
    public WebApplicationFactory<Program> Gateway { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Wallets = new WebApplicationFactory<WalletsProgram>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:WalletsDb", _connectionString);
                builder.UseSetting("Database:AutoMigrate", "true");
                builder.UseSetting("Jwt:Issuer", "test");
                builder.UseSetting("Jwt:Audience", "test");
                builder.UseSetting("Jwt:JwksUri", "http://localhost/unused");
                builder.UseSetting("Swagger:Enabled", "false");
                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                            TestAuthHandler.SchemeName, _ => { });
                });
            });

        Gateway = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<Yarp.ReverseProxy.Forwarder.IForwarderHttpClientFactory>(
                        new TestServerForwarderFactory(Wallets.Server.CreateHandler()));
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                            TestAuthHandler.SchemeName, _ => { });
                });
            });

        // прогрев Wallets: поднять сервер до запросов
        _ = Wallets.Server;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Gateway.Dispose();
        Wallets.Dispose();
        return Task.CompletedTask;
    }
}
