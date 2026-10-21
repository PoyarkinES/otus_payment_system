using Otus.PaymentSystem.Identity.Domain.Aggregates;

namespace Otus.PaymentSystem.Identity.Application.Queries.GetProfile;

/// <summary>Данные профиля пользователя.</summary>
public record GetProfileResult(Guid UserId, string Email, UserStatus Status, DateTimeOffset CreatedAt);
