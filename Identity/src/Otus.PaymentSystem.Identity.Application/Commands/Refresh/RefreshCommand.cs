using MediatR;
using Otus.PaymentSystem.Identity.Application.Commands.Auth;

namespace Otus.PaymentSystem.Identity.Application.Commands.Refresh
{
    /// <summary>Обновление сессии по refresh-токену.</summary>
    /// <param name="RefreshToken">Действующий refresh-токен.</param>
    public record RefreshCommand(string RefreshToken) : IRequest<AuthTokens>;
}
