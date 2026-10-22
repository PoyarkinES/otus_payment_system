using MediatR;

namespace Otus.PaymentSystem.Identity.Application.Queries.GetProfile;

/// <summary>Запрос профиля текущего пользователя по Id.</summary>
public record GetProfileQuery(Guid UserId) : IRequest<GetProfileResult>;
