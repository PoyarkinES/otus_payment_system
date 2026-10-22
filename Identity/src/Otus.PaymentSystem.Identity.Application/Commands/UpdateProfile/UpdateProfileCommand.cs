using MediatR;

namespace Otus.PaymentSystem.Identity.Application.Commands.UpdateProfile;

/// <summary>
/// Обновление профиля. Поля опциональны: null — не менять.
/// </summary>
public record UpdateProfileCommand(Guid UserId, string? NewEmail, string? NewPassword) : IRequest;
