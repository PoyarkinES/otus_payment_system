using MediatR;

namespace Otus.PaymentSystem.Identity.Application.Commands.Logout;

/// <summary>Завершение сессии по refresh-токену.</summary>
/// <param name="UserId">Пользователь из access-токена (claim sub).</param>
/// <param name="RefreshToken">Refresh-токен сессии, которую нужно завершить.</param>
public record LogoutCommand(Guid UserId, string RefreshToken) : IRequest<Unit>;
