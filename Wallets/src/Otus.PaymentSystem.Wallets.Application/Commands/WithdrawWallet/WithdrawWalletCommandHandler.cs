using MediatR;
using Otus.PaymentSystem.Wallets.Application.Contracts;
using Otus.PaymentSystem.Wallets.Application.Exceptions;
using Otus.PaymentSystem.Wallets.Application.Interfaces;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;

namespace Otus.PaymentSystem.Wallets.Application.Commands.WithdrawWallet;

public sealed class WithdrawWalletCommandHandler
    : IRequestHandler<WithdrawWalletCommand, WalletResult>
{
    private readonly IWalletRepository _wallets;

    public WithdrawWalletCommandHandler(IWalletRepository wallets) => _wallets = wallets;

    /// <summary>
    /// Withdraws funds from the user's wallet, using the idempotency key to avoid duplicate withdrawals.
    /// </summary>
    /// <remarks>A repeated idempotency key does not withdraw the funds again.</remarks>
    /// <param name="request">The withdrawal command containing the wallet, user, amount, and idempotency key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation.</param>
    /// <returns>A result containing the wallet after the withdrawal or the existing transaction is confirmed.</returns>
    public async Task<WalletResult> Handle(WithdrawWalletCommand request, CancellationToken cancellationToken)
    {
        var wallet = await GetOwnedWallet(request.WalletId, request.UserId, cancellationToken);

        var amount = Money.Create(request.Amount, wallet.Currency);
        var existingTransaction = wallet.Transactions
            .FirstOrDefault(t => t.ReferenceId == request.IdempotencyKey);

        if (existingTransaction is not null)
        {
            WalletOperationGuard.EnsureSameOperation(
                existingTransaction,
                WalletTransactionType.Withdrawal,
                amount.Amount);

            return WalletResult.From(wallet);
        }


        try
        {
            wallet.Withdraw(amount, request.IdempotencyKey);
            await _wallets.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateWalletOperationException)
        {
            var current = await GetOwnedWallet(
                request.WalletId,
                request.UserId,
                cancellationToken);

            var existing = current.Transactions
                .FirstOrDefault(t => t.ReferenceId == request.IdempotencyKey);

            if (existing is null)
            {
                throw new DomainException(
                    "Операция уже выполнена, но транзакция не найдена.",
                    DomainError.Conflict);
            }

            WalletOperationGuard.EnsureSameOperation(
                existing,
                WalletTransactionType.Withdrawal,
                amount.Amount);

            return WalletResult.From(current);
        }

        return WalletResult.From(wallet);
    }

    private async Task<Wallet> GetOwnedWallet(Guid walletId, Guid userId, CancellationToken ct)
    {
        var wallet = await _wallets.GetByIdAsync(walletId, ct);
        if (wallet is null)
            throw new DomainException("Кошелёк не найден.", DomainError.NotFound);
        if (wallet.UserId != userId)
            throw new DomainException("Доступ к чужому кошельку запрещён.", DomainError.Forbidden);
        return wallet;
    }
}
