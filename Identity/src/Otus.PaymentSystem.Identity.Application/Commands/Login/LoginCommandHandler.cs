using MediatR;
using Otus.PaymentSystem.Identity.Application.Commands.Auth;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Errors;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Application.Commands.Login;

/// <summary>
/// Обработчик аутентификации: находит пользователя по email, проверяет пароль,
/// проверяет статус и выпускает токен.
/// </summary>
public class LoginCommandHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenService tokens,
    IRefreshTokenFactory refreshTokens,
    IRefreshTokenRepository refreshRepository)
    : IRequestHandler<LoginCommand, AuthTokens>
{
    public async Task<AuthTokens> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = new Email(request.Email);

        User user = await users.GetByEmailAsync(email, cancellationToken)
                    ?? throw new DomainException("Неверные учётные данные.", DomainError.Unauthorized);

        if (!hasher.Verify(request.Password, user.PasswordHash))
            throw new DomainException("Неверные учётные данные.", DomainError.Unauthorized);

        if (user.Status == UserStatus.Blocked)
            throw new DomainException("Пользователь заблокирован.", DomainError.Forbidden);

        TokenResult access = tokens.GenerateToken(user);

        IssuedRefreshToken issued = refreshTokens.Issue();
        RefreshToken refresh = RefreshToken.Issues(user.Id, issued.Hash, issued.ExpiresAt);

        await refreshRepository.AddAsync(refresh, cancellationToken);
        await refreshRepository.SaveChangesAsync(cancellationToken);

        return new AuthTokens(access.AccessToken, access.ExpiresAt, issued.Raw, issued.ExpiresAt);
    }
}
