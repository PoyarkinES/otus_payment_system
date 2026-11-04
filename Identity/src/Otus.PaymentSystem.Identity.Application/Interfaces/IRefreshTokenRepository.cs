using Otus.PaymentSystem.Identity.Domain.Aggregates;

namespace Otus.PaymentSystem.Identity.Application.Interfaces;

/// <summary>Порт доступа к хранилищу refresh-токенов. Реализация — в Infrastructure.</summary>
public interface IRefreshTokenRepository
{
    /// <summary>Возвращает токен по SHA-256-хэшу или null, если не найден.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Возвращает токен по Id — нужен для обхода цепочки ротаций.</summary>
    Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Возвращает действующие токены пользователя (не отозваны, срок не истёк).</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(
        Guid userId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Добавляет новый токен.</summary>
    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>Фиксирует изменения, накопленные в трекнутых сущностях.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
