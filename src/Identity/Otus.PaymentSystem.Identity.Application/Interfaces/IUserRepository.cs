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

    /// <summary>Возвращает пользователя по Id или null, если не найден.</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Сохраняет нового пользователя.</summary>
    Task AddAsync(User user, CancellationToken ct = default);

    /// <summary>Фиксирует изменения, накопленные в трекнутых сущностях.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
