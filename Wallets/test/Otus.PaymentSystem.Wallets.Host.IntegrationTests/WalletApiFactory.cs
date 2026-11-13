using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace Otus.PaymentSystem.Wallets.Host.IntegrationTests;

public sealed class WalletApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public WalletApiFactory()
    {
        _connectionString = Environment.GetEnvironmentVariable("WALLETS_API_TEST_DB")
            ?? throw new InvalidOperationException(
                "Задайте WALLETS_API_TEST_DB (Host=localhost;Port=5433;Database=otus_wallets_api_test;Username=otus;Password=payments).");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:WalletsDb", _connectionString);
        builder.UseSetting("Database:AutoMigrate", "true");
        builder.UseSetting("Jwt:Issuer", "test");
        builder.UseSetting("Jwt:Audience", "test");
        builder.UseSetting("Jwt:JwksUri", "http://localhost/unused");

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
        });
    }
}
