using Otus.PaymentSystem.Identity.Domain.Aggregates;

namespace Otus.PaymentSystem.Identity.Application.Interfaces;

/// <summary>Результат генерации токена: сам токен и момент его истечения.</summary>
public record TokenResult(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Порт выпуска токенов аутентификации. Реализация — в Infrastructure.
/// </summary>
public interface ITokenService
{
    /// <summary>Выпускает токен для пользователя.</summary>
    TokenResult GenerateToken(User user);
}
