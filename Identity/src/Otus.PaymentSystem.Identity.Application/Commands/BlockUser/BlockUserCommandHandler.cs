using MediatR;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Exceptions;

namespace Otus.PaymentSystem.Identity.Application.Commands.BlockUser;

/// <summary>Блокирует пользователя. Инварианты — в User.Block.</summary>
public class BlockUserCommandHandler(IUserRepository users)
    : IRequestHandler<BlockUserCommand>
{
    public async Task Handle(BlockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.TargetUserId, cancellationToken)
            ?? throw new DomainException("Пользователь не найден.");

        user.Block(request.BlockedBy);
        await users.SaveChangesAsync(cancellationToken);
    }
}
