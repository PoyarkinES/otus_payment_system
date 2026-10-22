using FluentValidation;

namespace Otus.PaymentSystem.Identity.Host.Contracts.Auth;

/// <summary>Тело запроса POST /auth/register.</summary>
public record RegisterRequest(string Email, string Password);

/// <summary>Валидатор RegisterRequest: формат полей (не бизнес-правила).</summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email обязателен.")
            .EmailAddress().WithMessage("Некорректный формат email.")
            .MaximumLength(320);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен.")
            .MinimumLength(8).WithMessage("Пароль должен быть не короче 8 символов.");
    }
}
