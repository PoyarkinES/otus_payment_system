using Microsoft.EntityFrameworkCore;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;
using Otus.PaymentSystem.Identity.Infrastructure.Persistence;
using Otus.PaymentSystem.Identity.Infrastructure.Persistence.Repositories;

namespace Otus.PaymentSystem.Identity.Infrastructure.Tests;

public class EfUserRepositoryTests
{
    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }

    private static User CreateUser(string email = "user@example.com", string hash = "hash-1")
        => User.Register(new Email(email), hash);

    [Fact]
    public async Task AddAsync_ThenGetByEmail_ReturnsUser()
    {
        await using var db = CreateContext();
        var repo = new EfUserRepository(db);
        var user = CreateUser();

        await repo.AddAsync(user);
        await db.SaveChangesAsync();

        var found = await repo.GetByEmailAsync(user.Email);

        Assert.NotNull(found);
        Assert.Equal(user.Id, found!.Id);
        Assert.Equal("user@example.com", found.Email.Value);
        Assert.Equal("hash-1", found.PasswordHash);
    }

    [Fact]
    public async Task GetByEmailAsync_UnknownEmail_ReturnsNull()
    {
        await using var db = CreateContext();
        var repo = new EfUserRepository(db);

        var found = await repo.GetByEmailAsync(new Email("nobody@example.com"));

        Assert.Null(found);
    }

    [Fact]
    public async Task GetByEmailAsync_NormalizesEmailBeforeLookup()
    {
        await using var db = CreateContext();
        var repo = new EfUserRepository(db);
        var user = CreateUser("User@Example.COM");
        await repo.AddAsync(user);
        await db.SaveChangesAsync();

        var found = await repo.GetByEmailAsync(new Email("  user@example.com  "));

        Assert.NotNull(found);
    }
}
