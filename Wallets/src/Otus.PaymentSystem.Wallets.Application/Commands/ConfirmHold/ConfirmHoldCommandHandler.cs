using MediatR;
using Otus.PaymentSystem.Wallets.Application.Exceptions;
using Otus.PaymentSystem.Wallets.Application.Interfaces;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;

namespace Otus.PaymentSystem.Wallets.Application.Commands.ConfirmHold
{
    public sealed class ConfirmHoldCommandHandler(IWalletRepository wallets)
        : IRequestHandler<ConfirmHoldCommand, Unit>
    {
        public async Task<Unit> Handle(ConfirmHoldCommand request, CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    Wallet sender = await GetOwnedWallet(request.HoldId, request.UserId, cancellationToken);
                    Hold? hold = sender.Holds.FirstOrDefault(h => h.Id == request.HoldId);
                    if (hold == null)
                    {
                        throw new DomainException("Холд не найден.", DomainError.NotFound);
                    }

                    if (hold.Status == HoldStatus.Completed) return Unit.Value;
                    if (hold.Status != HoldStatus.Active)
                        throw new DomainException($"Холд в состоянии {hold.Status} нельзя исполнить.", DomainError.Conflict);

                    if (hold.RecipientUserId == Guid.Empty)
                        throw new DomainException("Холд без получателя нельзя исполнить.", DomainError.Conflict);

                    var recipient = await wallets.GetByUserAndCurrencyAsync(hold.RecipientUserId, sender.Currency, cancellationToken);
                    if (recipient == null)
                    {
                        recipient = Wallet.Create(hold.RecipientUserId, sender.Currency);
                        await wallets.AddAsync(recipient, cancellationToken);
                    }

                    if (!sender.ConfirmHold(hold.Id)) return Unit.Value;

                    recipient.ReceiveTransfer(hold.TransferId, Money.Create(hold.Amount, sender.Currency));
                    await wallets.SaveChangesAsync(cancellationToken);
                    return Unit.Value;
                }
                catch (DuplicateWalletException)
                    when (attempt < 2)
                {
                }
                catch (DuplicateWalletOperationException)
                    when (attempt < 2)
                {
                }
                catch (WalletConcurrencyException)
                    when (attempt < 2)
                {
                }
            }
        }

        private async Task<Wallet> GetOwnedWallet(Guid holdId, Guid userId, CancellationToken cancellationToken)
        {
            Wallet? wallet = await wallets.GetByHoldIdAsync(holdId, cancellationToken);
            if (wallet == null)
            {
                throw new DomainException("Холд не найден.", DomainError.NotFound);
            }

            if (wallet.UserId != userId)
            {
                throw new DomainException("Доступ к чужому кошельку запрещён.", DomainError.Forbidden);
            }

            return wallet;
        }

    }
}
