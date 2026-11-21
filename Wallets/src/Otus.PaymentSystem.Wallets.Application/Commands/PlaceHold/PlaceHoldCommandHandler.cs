using MediatR;
using Otus.PaymentSystem.Wallets.Application.Contracts;
using Otus.PaymentSystem.Wallets.Application.Exceptions;
using Otus.PaymentSystem.Wallets.Application.Interfaces;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;

namespace Otus.PaymentSystem.Wallets.Application.Commands.PlaceHold
{
    public sealed class PlaceHoldCommandHandler(IWalletRepository wallets)
        : IRequestHandler<PlaceHoldCommand, HoldResult>
    {
        public async Task<HoldResult> Handle(PlaceHoldCommand request, CancellationToken cancellationToken)
        {
            Wallet wallet = await GetOwnedWallet(request.WalletId, request.UserId, cancellationToken);

            var amount = Money.Create(request.Amount, wallet.Currency);
            if (amount.Amount <= 0m)
            {
                throw new DomainException("Сумма удержания должна быть положительной.", DomainError.Validation);
            }

            var existing = wallet.Holds.FirstOrDefault(h => h.TransferId == request.TransferId);
            if (existing is not null)
            {
                WalletOperationGuard.EnsureSameHoldParameters(
                    existing,
                    amount.Amount,
                    request.RecipientUserId);

                return HoldResult.From(existing, wallet.Currency);
            }

            try
            {
                var hold = wallet.PlaceHold(
                    request.TransferId,
                    amount,
                    request.UserId,
                    request.RecipientUserId);

                await wallets.SaveChangesAsync(cancellationToken);

                return HoldResult.From(hold, wallet.Currency);
            }
            catch (DuplicateHoldException)
            {
                var owner = await wallets.GetByTransferIdAsync(request.TransferId, cancellationToken);
                if (owner is null)
                    throw new DomainException("Холд с таким TransferId уже существует.", DomainError.Conflict);

                if (owner.UserId != request.UserId)
                    throw new DomainException("TransferId уже использован другим кошельком.", DomainError.Conflict);

                var racedHold = owner.Holds.FirstOrDefault(h => h.TransferId == request.TransferId);
                if (racedHold is null)
                    throw new DomainException("Холд с таким TransferId уже существует.", DomainError.Conflict);

                WalletOperationGuard.EnsureSameHoldParameters(
                    racedHold,
                    amount.Amount,
                    request.RecipientUserId);

                return HoldResult.From(racedHold, owner.Currency);
            }
        }

        private async Task<Wallet> GetOwnedWallet(Guid walletId, Guid userId, CancellationToken ct)
        {
            var wallet = await wallets.GetByIdAsync(walletId, ct);
            if (wallet is null)
                throw new DomainException("Кошелёк не найден.", DomainError.NotFound);
            if (wallet.UserId != userId)
                throw new DomainException("Доступ к чужому кошельку запрещён.", DomainError.Forbidden);

            return wallet;
        }

    }
}
