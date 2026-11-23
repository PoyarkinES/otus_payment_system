using MediatR;

namespace Otus.PaymentSystem.Wallets.Application.Commands.ConfirmHold
{
    public sealed record ConfirmHoldCommand(
        Guid HoldId,
        Guid UserId) : IRequest<Unit>;
}
