using Microsoft.EntityFrameworkCore;
using Npgsql;
using Otus.PaymentSystem.Identity.Infrastructure.Persistence;

namespace Otus.PaymentSystem.Identity.Infrastructure.Tests;

/// <summary>
/// Интеграционный fixture на реальном PostgreSQL.
/// Строка подключения берётся из IDENTITY_TEST_DB, чтобы тесты не зависели
/// от Docker Desktop named pipe (Testcontainers на Windows требует отдельной настройки).
/// База обязана содержать "test" в имени — защита от случайного удаления dev-базы.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string _connectionString;

    public PostgresFixture()
    {
        _connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_DB")
            ?? throw new InvalidOperationException(
                "Задайте IDENTITY_TEST_DB, например: " +
                "Host=localhost;Port=5433;Database=otus_identity_test;Username=otus;Password=payments");

        var database = new NpgsqlConnectionStringBuilder(_connectionString).Database;
        if (string.IsNullOrWhiteSpace(database) ||
            !database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"IDENTITY_TEST_DB должен указывать на тестовую базу, а не на '{database}'.");
        }
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        await using var db = new IdentityDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
