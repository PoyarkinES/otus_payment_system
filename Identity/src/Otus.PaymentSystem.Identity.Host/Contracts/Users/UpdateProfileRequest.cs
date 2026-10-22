using FluentValidation;

namespace Otus.PaymentSystem.Identity.Host.Contracts.Users;

/// <summary>Тело запроса PUT /users/me. Поля опциональны.</summary>
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
            .When(x => x.Password is not null);
    }
}
