using MediatR;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Application.Commands.Login;

/// <summary>
/// Обработчик аутентификации: находит пользователя по email, проверяет пароль,
/// проверяет статус и выпускает токен.
/// </summary>
public class LoginCommandHandler(IUserRepository users, IPasswordHasher hasher, ITokenService tokens)
    : IRequestHandler<LoginCommand, LoginResult>
{
    /// <inheritdoc />
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = new Email(request.Email);

        User user = await users.GetByEmailAsync(email, cancellationToken)
                    ?? throw new DomainException("Неверные учётные данные.");

        // Проверяем пароль ДО статуса, чтобы не раскрывать существование
        // и состояние аккаунта клиенту, который не доказал владение им.
        // Иначе перебором email'ов можно узнать, кто зарегистрирован
        // и кто заблокирован (user enumeration). До Verify ответы одинаковы.
        if (!hasher.Verify(request.Password, user.PasswordHash))
            throw new DomainException("Неверные учётные данные.");

        // Статус проверяем только после успешного Verify: клиент доказал
        // владение аккаунтом, и ему полезно знать причину отказа (UX).
        if (user.Status == UserStatus.Blocked)
            throw new DomainException("Пользователь заблокирован.");

        TokenResult token = tokens.GenerateToken(user);
        return new LoginResult(token.AccessToken, token.ExpiresAt);
    }
}
