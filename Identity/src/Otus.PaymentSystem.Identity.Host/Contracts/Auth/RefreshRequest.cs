using FluentValidation;

namespace Otus.PaymentSystem.Identity.Host.Contracts.Auth;

/// <summary>Тело запроса POST /auth/refresh.</summary>
/// <param name="RefreshToken">Токен обновления.</param>
public record RefreshRequest(string RefreshToken);

/// <summary>Валидатор RefreshRequest.</summary>
public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MaximumLength(512);
    }
}
