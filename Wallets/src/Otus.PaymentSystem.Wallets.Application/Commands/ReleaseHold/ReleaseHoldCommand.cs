using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Otus.PaymentSystem.Wallets.Application.Commands.ReleaseHold
{
    public sealed record ReleaseHoldCommand(Guid HoldId, Guid UserId) : IRequest<Unit>;
}
