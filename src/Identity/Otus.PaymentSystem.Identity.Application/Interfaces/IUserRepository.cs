using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Application.Interfaces;

/// <summary>
/// Порт доступа к хранилищу пользователей. Реализация — в Infrastructure.
/// </summary>
public interface IUserRepository
{
    /// <summary>Возвращает пользователя по email или null, если не найден.</summary>
    Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default);

    /// <summary>Сохраняет нового пользователя.</summary>
    Task AddAsync(User user, CancellationToken ct = default);
}
