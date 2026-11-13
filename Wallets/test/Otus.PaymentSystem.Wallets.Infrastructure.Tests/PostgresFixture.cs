using Microsoft.EntityFrameworkCore;
using Npgsql;
using Otus.PaymentSystem.Wallets.Infrastructure.Persistence;
using System;
using System.Threading.Tasks;

namespace Otus.PaymentSystem.Wallets.Infrastructure.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string _connectionString;

    public PostgresFixture()
    {
        _connectionString = Environment.GetEnvironmentVariable("WALLETS_REPO_TEST_DB")
            ?? throw new InvalidOperationException(
                "Задайте WALLETS_REPO_TEST_DB (Host=localhost;Port=5433;Database=otus_wallets_repo_test;Username=otus;Password=payments).");

        var database = new NpgsqlConnectionStringBuilder(_connectionString).Database;
        if (string.IsNullOrWhiteSpace(database) ||
            !database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"WALLETS_TEST_DB должен указывать на тестовую базу, а не на '{database}'.");
        }
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<WalletsDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        await using var db = new WalletsDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
