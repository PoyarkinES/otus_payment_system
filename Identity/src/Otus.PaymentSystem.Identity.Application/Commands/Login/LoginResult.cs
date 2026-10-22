namespace Otus.PaymentSystem.Identity.Application.Commands.Login;

/// <summary>Результат успешной аутентификации.</summary>
/// <param name="AccessToken">JWT access-токен.</param>
/// <param name="ExpiresAt">Момент истечения токена.</param>
public record LoginResult(string AccessToken, DateTimeOffset ExpiresAt);
