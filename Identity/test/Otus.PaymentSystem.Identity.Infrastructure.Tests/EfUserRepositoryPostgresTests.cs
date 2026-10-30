using Microsoft.EntityFrameworkCore;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;
using Otus.PaymentSystem.Identity.Infrastructure.Persistence;
using Otus.PaymentSystem.Identity.Infrastructure.Persistence.Repositories;

namespace Otus.PaymentSystem.Identity.Infrastructure.Tests;

/// <summary>
/// Интеграционные тесты на реальном PostgreSQL.
/// В отличие от InMemory, проверяют миграции, типы PostgreSQL и реальный SaveChanges.
/// </summary>
public sealed class EfUserRepositoryPostgresTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task AddAsync_ThenSaveAndReopen_PersistsUserInPostgres()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using (var db = new IdentityDbContext(options))
        {
            await db.Database.MigrateAsync();

            var repository = new EfUserRepository(db);
            var user = User.Register(new Email("postgres@example.com"), "hash-postgres");

            await repository.AddAsync(user);
            await repository.SaveChangesAsync();
        }

        // Новый контекст — читаем именно из БД, а не из change tracker.
        await using (var db = new IdentityDbContext(options))
        {
            var repository = new EfUserRepository(db);
            var found = await repository.GetByEmailAsync(new Email("postgres@example.com"));

            Assert.NotNull(found);
            Assert.Equal("postgres@example.com", found!.Email.Value);
            Assert.Equal("hash-postgres", found.PasswordHash);
            Assert.Equal(UserRole.User, found.Role);
            Assert.Equal(UserStatus.Active, found.Status);
        }
    }

    [Fact]
    public async Task AddAsync_DuplicateEmail_ThrowsOnUniqueIndex()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var db = new IdentityDbContext(options);
        await db.Database.MigrateAsync();

        var repository = new EfUserRepository(db);
        await repository.AddAsync(User.Register(new Email("duplicate@example.com"), "hash-1"));
        await repository.SaveChangesAsync();

        await repository.AddAsync(User.Register(new Email("duplicate@example.com"), "hash-2"));

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());
    }
}
