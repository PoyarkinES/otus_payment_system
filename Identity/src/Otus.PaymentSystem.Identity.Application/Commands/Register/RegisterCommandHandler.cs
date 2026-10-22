using MediatR;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Application.Commands.Register;

/// <summary>
/// Обработчик регистрации: проверяет уникальность email, хеширует пароль,
/// создаёт агрегат User и сохраняет его.
/// </summary>
public class RegisterCommandHandler(IUserRepository users, IPasswordHasher hasher)
    : IRequestHandler<RegisterCommand, RegisterResult>
{
    /// <inheritdoc />
    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = new Email(request.Email);

        // Проверяем уникальность ДО хеширования: Hash — намеренно дорогая
        // операция (PBKDF2/Argon2). Делать её ради того, чтобы потом выбросить
        // результат с "Email уже занят", — пустая трата CPU (и вектор DoS).
        if (await users.GetByEmailAsync(email, cancellationToken) is not null)
            throw new DomainException("Email уже занят.");

        var hash = hasher.Hash(request.Password);
        var user = User.Register(email, hash);

        await users.AddAsync(user, cancellationToken);

        return new RegisterResult(user.Id, user.Email.Value);
    }
}
