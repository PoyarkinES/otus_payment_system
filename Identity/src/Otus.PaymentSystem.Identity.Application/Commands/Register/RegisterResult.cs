namespace Otus.PaymentSystem.Identity.Application.Commands.Register;

/// <summary>Результат успешной регистрации.</summary>
/// <param name="UserId">Идентификатор созданного пользователя.</param>
/// <param name="Email">Нормализованный email.</param>
public record RegisterResult(Guid UserId, string Email);
