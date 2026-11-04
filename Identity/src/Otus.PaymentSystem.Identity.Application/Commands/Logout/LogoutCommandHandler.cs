using MediatR;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;

namespace Otus.PaymentSystem.Identity.Application.Commands.Logout;

/// <summary>
/// Завершает сессию: отзывает предъявленный refresh-токен.
/// Идемпотентно — неизвестный, уже отозванный или чужой токен не считается ошибкой.
/// </summary>
public class LogoutCommandHandler(
    IRefreshTokenFactory factory,
    IRefreshTokenRepository repository)
    : IRequestHandler<LogoutCommand, Unit>
{
    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        string hash = factory.Hash(request.RefreshToken);
        RefreshToken? token = await repository.GetByHashAsync(hash, cancellationToken);

        // Чужой токен не трогаем: иначе владелец одного access-токена
        // мог бы завершить чужую сессию. Ответ всё равно 204, чтобы не давать оракула.
        if (token is null || token.UserId != request.UserId || token.RevokedAt is not null)
            return Unit.Value;

        token.Revoke();
        await repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
