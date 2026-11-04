using MediatR;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Errors;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Application.Commands.UpdateProfile;

/// <summary>
/// Меняет email и/или пароль. Пароль хешируется через IPasswordHasher.
/// Требует SaveChangesAsync (EF трекает изменения агрегата).
/// </summary>
public class UpdateProfileCommandHandler(IUserRepository users, IPasswordHasher hasher, IRefreshTokenRepository refreshTokens)
    : IRequestHandler<UpdateProfileCommand>
{
    public async Task Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new DomainException("Пользователь не найден.", DomainError.NotFound);

        if (request.NewEmail is not null)
        {
            var email = new Email(request.NewEmail);
            var existing = await users.GetByEmailAsync(email, cancellationToken);
            if (existing is not null && existing.Id != user.Id)
                throw new DomainException("Email уже занят.", DomainError.Conflict);
            user.ChangeEmail(email);
        }

        if (request.NewPassword is not null)
        {
            user.ChangePassword(hasher.Hash(request.NewPassword));

            // Смена пароля завершает все сессии: иначе украденный refresh-токен
            // пережил бы смену пароля, и смысл смены терялся бы.
            IReadOnlyList<RefreshToken> active = await refreshTokens.GetActiveByUserIdAsync(
                user.Id, DateTimeOffset.UtcNow, cancellationToken);

            foreach (RefreshToken token in active)
                token.Revoke();
        }

        // Оба репозитория работают с одним scoped DbContext, поэтому одного
        // SaveChanges достаточно: изменения пользователя и токенов уйдут одной транзакцией.
        await users.SaveChangesAsync(cancellationToken);
    }
}
