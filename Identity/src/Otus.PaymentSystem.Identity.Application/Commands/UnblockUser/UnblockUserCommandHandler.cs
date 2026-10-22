using MediatR;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Exceptions;

namespace Otus.PaymentSystem.Identity.Application.Commands.UnblockUser;

/// <summary>Разблокирует пользователя. Инварианты — в User.Unblock.</summary>
public class UnblockUserCommandHandler(IUserRepository users)
    : IRequestHandler<UnblockUserCommand>
{
    public async Task Handle(UnblockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.TargetUserId, cancellationToken)
            ?? throw new DomainException("Пользователь не найден.");

        user.Unblock(request.UnblockedBy);
        await users.SaveChangesAsync(cancellationToken);
    }
}
