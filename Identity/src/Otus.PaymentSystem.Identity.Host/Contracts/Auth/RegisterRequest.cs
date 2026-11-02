using FluentValidation;

namespace Otus.PaymentSystem.Identity.Host.Contracts.Auth;

/// <summary>Тело запроса POST /auth/register.</summary>
/// <param name="Email">Email, до 320 символов.</param>
/// <param name="Password">Пароль: 8–128 символов, минимум одна буква и одна цифра.</param>
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
            .MinimumLength(8).WithMessage("Пароль должен быть не короче 8 символов.")
            .MaximumLength(128).WithMessage("Пароль должен быть не длиннее 128 символов.")
            .Matches("[A-Za-z]").WithMessage("Пароль должен содержать хотя бы одну букву.")
            .Matches("[0-9]").WithMessage("Пароль должен содержать хотя бы одну цифру.");
    }
}
