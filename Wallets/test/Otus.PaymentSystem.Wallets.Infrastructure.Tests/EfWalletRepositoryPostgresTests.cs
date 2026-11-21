using Microsoft.EntityFrameworkCore;
using Otus.PaymentSystem.Wallets.Application.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
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
        var recipientId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var wallet = Wallet.Create(userId, Currency.Rub);
            wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
            wallet.PlaceHold(transferId, Money.Create(30m, Currency.Rub), userId, recipientId);

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
        var recipientId = Guid.NewGuid();       
        Guid walletId;

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var wallet = Wallet.Create(userId, Currency.Rub);
            wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
            wallet.PlaceHold(transferId, Money.Create(30m, Currency.Rub), userId, recipientId);
            await repo.AddAsync(wallet);
            await repo.SaveChangesAsync();
            walletId = wallet.Id;
        }

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var loaded = await repo.GetByIdAsync(walletId);

            Assert.NotNull(loaded);
            loaded!.PlaceHold(transferId, Money.Create(30m, Currency.Rub), userId, recipientId);

            Assert.Single(loaded.Holds); // идемпотентность после Include(Holds)
            Assert.Equal(70m, loaded.Available);
            Assert.Equal(30m, loaded.Held);
        }
    }

    [Fact]
    public async Task SaveChanges_DuplicateTransferId_ThrowsDuplicateHoldException()
    {
        var userId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var wallet = Wallet.Create(userId, Currency.Rub);
            wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
            wallet.PlaceHold(transferId, Money.Create(30m, Currency.Rub), userId, recipientId);
            await repo.AddAsync(wallet);
            await repo.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var other = Wallet.Create(Guid.NewGuid(), Currency.Rub);
            other.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
            other.PlaceHold(transferId, Money.Create(10m, Currency.Rub), other.UserId, recipientId);
            await repo.AddAsync(other);

            await Assert.ThrowsAsync<DuplicateHoldException>(() => repo.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task GetByHoldIdAsync_ReturnsWalletWithHoldAndTransactions()
    {
        var userId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        Guid walletId;
        Guid holdId;

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var wallet = Wallet.Create(userId, Currency.Rub);
            wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
            var hold = wallet.PlaceHold(transferId, Money.Create(30m, Currency.Rub), userId, recipientId);
            await repo.AddAsync(wallet);
            await repo.SaveChangesAsync();
            walletId = wallet.Id;
            holdId = hold.Id;
        }

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var loaded = await repo.GetByHoldIdAsync(holdId);

            Assert.NotNull(loaded);
            Assert.Equal(walletId, loaded!.Id);
            Assert.Single(loaded.Holds);
            Assert.Equal(recipientId, Assert.Single(loaded.Holds).RecipientUserId);
            Assert.Single(loaded.Transactions);
        }
    }

    [Fact]
    public async Task GetByTransferIdAsync_ReturnsOwningWallet()
    {
        var userId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        Guid walletId;

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var wallet = Wallet.Create(userId, Currency.Rub);
            wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
            wallet.PlaceHold(transferId, Money.Create(30m, Currency.Rub), userId, recipientId);
            await repo.AddAsync(wallet);
            await repo.SaveChangesAsync();
            walletId = wallet.Id;
        }

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var loaded = await repo.GetByTransferIdAsync(transferId);

            Assert.NotNull(loaded);
            Assert.Equal(walletId, loaded!.Id);
            Assert.Single(loaded.Holds);
            Assert.Equal(recipientId, Assert.Single(loaded.Holds).RecipientUserId);
        }
    }

    [Fact]
    public async Task GetByHoldIdAsync_UnknownId_ReturnsNull()
    {
        await using var db = CreateDbContext();
        var repo = new EfWalletRepository(db);

        Assert.Null(await repo.GetByHoldIdAsync(Guid.NewGuid()));
        Assert.Null(await repo.GetByTransferIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task SaveChanges_DuplicateUserCurrency_ThrowsDuplicateWalletException()
    {
        var userId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var repo = new EfWalletRepository(db);
        await repo.AddAsync(Wallet.Create(userId, Currency.Rub));
        await repo.SaveChangesAsync();

        await repo.AddAsync(Wallet.Create(userId, Currency.Rub));

        await Assert.ThrowsAsync<DuplicateWalletException>(() => repo.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_ConcurrentUpdate_ThrowsWalletConcurrencyException()
    {
        var userId = Guid.NewGuid();
        Guid walletId;

        await using (var db = CreateDbContext())
        {
            var repo = new EfWalletRepository(db);
            var wallet = Wallet.Create(userId, Currency.Rub);
            wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
            await repo.AddAsync(wallet);
            await repo.SaveChangesAsync();
            walletId = wallet.Id;
        }

        await using var dbB = CreateDbContext();
        await using var dbC = CreateDbContext();
        var repoB = new EfWalletRepository(dbB);
        var repoC = new EfWalletRepository(dbC);
        var walletB = await repoB.GetByIdAsync(walletId);
        var walletC = await repoC.GetByIdAsync(walletId);

        Assert.NotNull(walletB);
        Assert.NotNull(walletC);
        walletB!.Deposit(Money.Create(10m, Currency.Rub), Guid.NewGuid());
        walletC!.Deposit(Money.Create(20m, Currency.Rub), Guid.NewGuid());

        await repoB.SaveChangesAsync();

        await Assert.ThrowsAsync<WalletConcurrencyException>(() => repoC.SaveChangesAsync());
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
