using MediatR;

namespace Otus.PaymentSystem.Identity.Application.Commands.Register;

/// <summary>
/// Команда регистрации нового пользователя.
/// </summary>
/// <param name="Email">Email в свободной форме; нормализуется в VO Email.</param>
/// <param name="Password">Пароль в открытом виде; хешируется в handler'е.</param>
public record RegisterCommand(string Email, string Password) : IRequest<RegisterResult>;
