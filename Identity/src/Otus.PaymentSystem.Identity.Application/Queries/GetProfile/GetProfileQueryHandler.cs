using MediatR;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Exceptions;

namespace Otus.PaymentSystem.Identity.Application.Queries.GetProfile;

/// <summary>Возвращает профиль по Id. Бросает DomainException, если пользователь не найден.</summary>
public class GetProfileQueryHandler(IUserRepository users)
    : IRequestHandler<GetProfileQuery, GetProfileResult>
{
    public async Task<GetProfileResult> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new DomainException("Пользователь не найден.");

        return new GetProfileResult(user.Id, user.Email.Value, user.Status, user.CreatedAt);
    }
}
