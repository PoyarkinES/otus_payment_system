using FluentValidation;

namespace Otus.PaymentSystem.Identity.Host.Contracts.Users;

/// <summary>Тело запроса PUT /users/me. Поля опциональны.</summary>
/// <param name="Email">Новый email, до 320 символов. Если null — не меняется.</param>
/// <param name="Password">Новый пароль: 8–128 символов, буква и цифра. Если null — не меняется.</param>
public record UpdateProfileRequest(string? Email, string? Password);

/// <summary>Валидатор: если поле задано — оно должно быть валидным.</summary>
public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Некорректный формат email.")
            .MaximumLength(320)
            .When(x => x.Email is not null);

        RuleFor(x => x.Password)
            .MinimumLength(8).WithMessage("Пароль должен быть не короче 8 символов.")
            .MaximumLength(128).WithMessage("Пароль должен быть не длиннее 128 символов.")
            .Matches("[A-Za-z]").WithMessage("Пароль должен содержать хотя бы одну букву.")
            .Matches("[0-9]").WithMessage("Пароль должен содержать хотя бы одну цифру.")
            .When(x => x.Password is not null);
    }
}
