namespace Otus.PaymentSystem.Identity.Application.Commands.Auth;

/// <summary>Пара токенов, которую клиент получает при входе и при обновлении сессии.</summary>
/// <param name="AccessToken">JWT access-токен.</param>
/// <param name="ExpiresAt">Момент истечения access-токена.</param>
/// <param name="RefreshToken">Refresh-токен: 32 случайных байта, base64url; в БД только SHA-256.</param>
/// <param name="RefreshExpiresAt">Момент истечения refresh-токена.</param>
public record AuthTokens(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt);
