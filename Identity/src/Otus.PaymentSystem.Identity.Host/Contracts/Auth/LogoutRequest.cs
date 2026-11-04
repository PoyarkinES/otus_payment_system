using FluentValidation;

namespace Otus.PaymentSystem.Identity.Host.Contracts.Auth;

/// <summary>Тело запроса POST /auth/logout.</summary>
/// <param name="RefreshToken">refresh-токен сессии, которую нужно завершить.</param>
public record LogoutRequest(string RefreshToken);

/// <summary>Валидатор LogoutRequest.</summary>
public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MaximumLength(512);
    }
}
