using MediatR;
using Otus.PaymentSystem.Wallets.Application.Exceptions;
using Otus.PaymentSystem.Wallets.Application.Interfaces;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Otus.PaymentSystem.Wallets.Application.Commands.ReleaseHold
{
    public sealed class ReleaseHoldCommandHandler(IWalletRepository wallets) : IRequestHandler<ReleaseHoldCommand, Unit>
    {
        public async Task<Unit> Handle(ReleaseHoldCommand request, CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {

                try

                {
                    Wallet? wallet = await wallets.GetByHoldIdAsync(request.HoldId, cancellationToken);
                    if (wallet == null)
                    {
                        throw new DomainException("Холд не найден", DomainError.NotFound);
                    }

                    if (wallet.UserId != request.UserId)
                    {
                        throw new DomainException("Нет доступа к холду", DomainError.Forbidden);
                    }

                    Hold? hold = wallet.Holds.FirstOrDefault(h => h.Id == request.HoldId);
                    if (hold == null)
                    {
                        throw new DomainException("Холд не найден", DomainError.NotFound);
                    }

                    if (hold.Status == HoldStatus.Released)
                    {
                        return Unit.Value;
                    }

                    if (hold.Status != HoldStatus.Active)
                    {
                        throw new DomainException("Холд не найден", DomainError.Conflict);
                    }

                    wallet.ReleaseHold(hold.Id);
                    await wallets.SaveChangesAsync(cancellationToken);
                    return Unit.Value;
                }
                catch (WalletConcurrencyException) when (attempt < 2) { }
            }
        }
    }
}
