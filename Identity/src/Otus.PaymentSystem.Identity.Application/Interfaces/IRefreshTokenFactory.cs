namespace Otus.PaymentSystem.Identity.Application.Interfaces;

/// <summary>Выпущенный refresh-токен: открытое значение клиенту, хэш для БД и срок истечения.</summary>
public record IssuedRefreshToken(string Raw, string Hash, DateTimeOffset ExpiresAt);

/// <summary>
/// Порт выпуска refresh-токенов. Реализация — в Infrastructure: криптография в Domain не живёт.
/// </summary>
public interface IRefreshTokenFactory
{
    /// <summary>Выпускает токен: случайные байты, их SHA-256 и срок из настроек.</summary>
    IssuedRefreshToken Issue();

    /// <summary>Считает SHA-256 для предъявленного токена, чтобы найти строку в БД по хэшу.</summary>
    string Hash(string raw);
}
