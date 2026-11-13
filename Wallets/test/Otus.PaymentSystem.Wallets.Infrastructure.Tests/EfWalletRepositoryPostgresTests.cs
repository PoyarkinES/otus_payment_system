using Microsoft.EntityFrameworkCore;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;
using Otus.PaymentSystem.Wallets.Infrastructure.Persistence;

namespace Otus.PaymentSystem.Wallets.Infrastructure.Tests;

public class EfWalletRepositoryPostgresTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public EfWalletRepositoryPostgresTests(PostgresFixture fixture) => _fixture = fixture;

    private WalletsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WalletsDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new WalletsDbContext(options);
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsWalletWithHoldsAndTransactions()
    {
        var userId = Guid.NewGuid();
        var transferId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var wallet = Wallet.Create(userId, Currency.Rub);
            wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
            wallet.PlaceHold(transferId, Money.Create(30m, Currency.Rub), userId);

            await repo.AddAsync(wallet);
            await repo.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var loaded = await repo.GetByIdAsync(
                (await db.Wallets.AsNoTracking().SingleAsync(w => w.UserId == userId)).Id);

            Assert.NotNull(loaded);
            Assert.Equal(70m, loaded!.Available);
            Assert.Equal(30m, loaded.Held);
            Assert.Single(loaded.Holds);        // Include(Holds) — runtime-вопрос §6.3
            Assert.Single(loaded.Transactions); // Include(Transactions) — только TopUp
        }
    }

    [Fact]
    public async Task PlaceHold_WithSameTransferId_OnReloadedWallet_ReturnsExistingHold()
    {
        var userId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        Guid walletId;

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var wallet = Wallet.Create(userId, Currency.Rub);
            wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
            wallet.PlaceHold(transferId, Money.Create(30m, Currency.Rub), userId);
            await repo.AddAsync(wallet);
            await repo.SaveChangesAsync();
            walletId = wallet.Id;
        }

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var loaded = await repo.GetByIdAsync(walletId);

            Assert.NotNull(loaded);
            var second = loaded!.PlaceHold(transferId, Money.Create(30m, Currency.Rub), userId);

            Assert.Single(loaded.Holds); // идемпотентность после Include(Holds)
            Assert.Equal(70m, loaded.Available);
            Assert.Equal(30m, loaded.Held);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_DuplicateTransferId_ThrowsDomainExceptionConflict()
    {
        var userId = Guid.NewGuid();
        var transferId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var wallet = Wallet.Create(userId, Currency.Rub);
            wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
            wallet.PlaceHold(transferId, Money.Create(30m, Currency.Rub), userId);
            await repo.AddAsync(wallet);
            await repo.SaveChangesAsync();
        }

        // второй кошелёк с тем же TransferId → UNIQUE(Holds.TransferId) → DbUpdateException → DomainException
        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var other = Wallet.Create(Guid.NewGuid(), Currency.Rub);
            other.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
            other.PlaceHold(transferId, Money.Create(10m, Currency.Rub), other.UserId);
            await repo.AddAsync(other);

            await Assert.ThrowsAsync<DomainException>(() => repo.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueForExistingWallet()
    {
        var userId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            await repo.AddAsync(Wallet.Create(userId, Currency.Rub));
            await repo.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            Assert.True(await repo.ExistsAsync(userId, Currency.Rub));
            Assert.False(await repo.ExistsAsync(Guid.NewGuid(), Currency.Rub));
        }
    }
}
