using FluentValidation;

namespace Otus.PaymentSystem.Identity.Host.Contracts.Auth;

/// <summary>Тело запроса POST /auth/login.</summary>
/// <param name="Email">Email, до 320 символов.</param>
/// <param name="Password">Пароль пользователя. Сложность здесь не проверяется.</param>
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
