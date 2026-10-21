using FluentValidation;

namespace Otus.PaymentSystem.Identity.Host.Contracts.Auth;

/// <summary>Тело запроса POST /auth/login.</summary>
public record LoginRequest(string Email, string Password);

/// <summary>Валидатор LoginRequest.</summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty();
    }
}
