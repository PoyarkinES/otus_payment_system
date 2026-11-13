using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.Events;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;
using System;

namespace Otus.PaymentSystem.Wallets.Domain.Tests;

public class WalletTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static Wallet CreateWallet() => Wallet.Create(UserId, Currency.Rub);

    [Fact]
    public void Create_WithValidData_CreatesActiveWalletWithZeroBalances()
    {
        var wallet = CreateWallet();

        Assert.NotEqual(Guid.Empty, wallet.Id);
        Assert.Equal(UserId, wallet.UserId);
        Assert.Equal("RUB", wallet.Currency.Value);
        Assert.Equal(0m, wallet.Available);
        Assert.Equal(0m, wallet.Held);
        Assert.Equal(0m, wallet.Total);
        Assert.Equal(WalletStatus.Active, wallet.Status);
        Assert.Equal(0, wallet.Version);
        Assert.Contains(wallet.DomainEvents, e => e is WalletCreated);
    }

    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        Assert.Throws<DomainException>(() => Wallet.Create(Guid.Empty, Currency.Rub));
    }

    [Fact]
    public void Deposit_IncreasesAvailable_AndWritesTopUpLedger()
    {
        var wallet = CreateWallet();
        var amount = Money.Create(100m, Currency.Rub);

        wallet.Deposit(amount, Guid.NewGuid());

        Assert.Equal(100m, wallet.Available);
        Assert.Equal(0m, wallet.Held);
        var tx = Assert.Single(wallet.Transactions);
        Assert.Equal(WalletTransactionType.TopUp, tx.Type);
        Assert.Equal(100m, tx.Amount);
        Assert.Equal(0m, tx.BalanceBefore);
        Assert.Equal(100m, tx.BalanceAfter);
    }

    [Fact]
    public void Deposit_WithZeroAmount_Throws()
    {
        var wallet = CreateWallet();
        var zero = Money.Create(0m, Currency.Rub);

        Assert.Throws<DomainException>(() => wallet.Deposit(zero, Guid.NewGuid()));
    }

    [Fact]
    public void Withdraw_WithInsufficientFunds_Throws()
    {
        var wallet = CreateWallet();

        Assert.Throws<DomainException>(() =>
            wallet.Withdraw(Money.Create(100m, Currency.Rub), Guid.NewGuid()));
    }

    [Fact]
    public void Withdraw_WithSufficientFunds_DecreasesAvailable_AndWritesWithdrawalLedger()
    {
        var wallet = CreateWallet();
        wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());

        wallet.Withdraw(Money.Create(40m, Currency.Rub), Guid.NewGuid());

        Assert.Equal(60m, wallet.Available);
        Assert.Equal(0m, wallet.Held);
        Assert.Equal(2, wallet.Transactions.Count);
        Assert.Contains(wallet.Transactions, t => t.Type == WalletTransactionType.Withdrawal && t.Amount == 40m);
    }

    [Fact]
    public void PlaceHold_MovesAvailableToHeld_AndCreatesActiveHold()
    {
        var wallet = CreateWallet();
        wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
        var transferId = Guid.NewGuid();

        var hold = wallet.PlaceHold(transferId, Money.Create(30m, Currency.Rub), UserId);

        Assert.Equal(70m, wallet.Available);
        Assert.Equal(30m, wallet.Held);
        Assert.Equal(HoldStatus.Active, hold.Status);
        Assert.Equal(transferId, hold.TransferId);
        Assert.Single(wallet.Holds);
        Assert.Single(wallet.Transactions); // только TopUp: hold ledger не пишет
    }

    [Fact]
    public void PlaceHold_WithSameTransferId_ReturnsExistingHold_AndDoesNotChangeBalances()
    {
        var wallet = CreateWallet();
        wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
        var transferId = Guid.NewGuid();

        var first = wallet.PlaceHold(transferId, Money.Create(30m, Currency.Rub), UserId);
        var second = wallet.PlaceHold(transferId, Money.Create(30m, Currency.Rub), UserId);

        Assert.Same(first, second);
        Assert.Single(wallet.Holds);
        Assert.Equal(70m, wallet.Available);
        Assert.Equal(30m, wallet.Held);
    }

    [Fact]
    public void PlaceHold_WithInsufficientAvailable_Throws()
    {
        var wallet = CreateWallet();
        wallet.Deposit(Money.Create(10m, Currency.Rub), Guid.NewGuid());

        Assert.Throws<DomainException>(() =>
            wallet.PlaceHold(Guid.NewGuid(), Money.Create(30m, Currency.Rub), UserId));
    }

    [Fact]
    public void ReleaseHold_ReturnsHeldToAvailable_AndWritesNoLedger()
    {
        var wallet = CreateWallet();
        wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
        var hold = wallet.PlaceHold(Guid.NewGuid(), Money.Create(30m, Currency.Rub), UserId);

        wallet.ReleaseHold(hold.Id);

        Assert.Equal(100m, wallet.Available);
        Assert.Equal(0m, wallet.Held);
        Assert.Equal(HoldStatus.Released, hold.Status);
        Assert.Single(wallet.Transactions); // только TopUp
    }

    [Fact]
    public void ConfirmHold_MovesHeldOut_AndWritesTransferOutLedger()
    {
        var wallet = CreateWallet();
        wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
        var hold = wallet.PlaceHold(Guid.NewGuid(), Money.Create(30m, Currency.Rub), UserId);

        wallet.ConfirmHold(hold.Id);

        Assert.Equal(70m, wallet.Available);
        Assert.Equal(0m, wallet.Held);
        Assert.Equal(HoldStatus.Completed, hold.Status);
        Assert.Contains(wallet.Transactions, t => t.Type == WalletTransactionType.TransferOut && t.Amount == 30m);
    }

    [Fact]
    public void ConfirmHold_AlreadyCompleted_IsIdempotent()
    {
        var wallet = CreateWallet();
        wallet.Deposit(Money.Create(100m, Currency.Rub), Guid.NewGuid());
        var hold = wallet.PlaceHold(Guid.NewGuid(), Money.Create(30m, Currency.Rub), UserId);
        wallet.ConfirmHold(hold.Id);
        var txCount = wallet.Transactions.Count;

        wallet.ConfirmHold(hold.Id);

        Assert.Equal(txCount, wallet.Transactions.Count);
        Assert.Equal(70m, wallet.Available);
    }

    [Fact]
    public void ReceiveTransfer_IncreasesAvailable_AndWritesTransferInLedger()
    {
        var wallet = CreateWallet();
        var transferId = Guid.NewGuid();

        wallet.ReceiveTransfer(transferId, Money.Create(50m, Currency.Rub));

        Assert.Equal(50m, wallet.Available);
        Assert.Contains(wallet.Transactions, t => t.Type == WalletTransactionType.TransferIn && t.Amount == 50m);
    }
}
