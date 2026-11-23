using System;
using System.Collections.Generic;
using System.Text;
using MediatR;
using Otus.PaymentSystem.Wallets.Application.Contracts;

namespace Otus.PaymentSystem.Wallets.Application.Commands.PlaceHold
{
    public sealed record PlaceHoldCommand(
        Guid WalletId,
        Guid UserId,
        Guid TransferId,
        decimal Amount,
        Guid RecipientUserId) : IRequest<HoldResult>;
}
