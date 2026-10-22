using MediatR;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Application.Commands.UpdateProfile;

/// <summary>
/// Меняет email и/или пароль. Пароль хешируется через IPasswordHasher.
/// Требует SaveChangesAsync (EF трекает изменения агрегата).
/// </summary>
public class UpdateProfileCommandHandler(IUserRepository users, IPasswordHasher hasher)
    : IRequestHandler<UpdateProfileCommand>
{
    public async Task Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new DomainException("Пользователь не найден.");

        if (request.NewEmail is not null)
        {
            var email = new Email(request.NewEmail);
            var existing = await users.GetByEmailAsync(email, cancellationToken);
            if (existing is not null && existing.Id != user.Id)
                throw new DomainException("Email уже занят.");
            user.ChangeEmail(email);
        }

        if (request.NewPassword is not null)
            user.ChangePassword(hasher.Hash(request.NewPassword));

        await users.SaveChangesAsync(cancellationToken);
    }
}
