using Otus.PaymentSystem.Wallets.Application.Contracts;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;

namespace Otus.PaymentSystem.Wallets.Application.Interfaces;

public interface IWalletRepository
{
    /// <summary>
    /// Получает кошелёк по его идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор кошелька.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Найденный кошелёк или <see langword="null" />, если кошелёк не существует.</returns>
    Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает кошелёк по идентификатору холда.
    /// </summary>
    /// <param name="holdId">Идентификатор холда.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Связанный кошелёк или <see langword="null" />, если кошелёк не найден.</returns>
    Task<Wallet?> GetByHoldIdAsync(Guid holdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает кошелёк по идентификатору перевода.
    /// </summary>
    /// <param name="transferId">Идентификатор перевода.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Связанный кошелёк или <see langword="null" />, если кошелёк не найден.</returns>
    Task<Wallet?> GetByTransferIdAsync(Guid transferId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает кошелёк пользователя в указанной валюте.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="currency">Валюта кошелька.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Найденный кошелёк или <see langword="null" />, если кошелёк не существует.</returns>
    Task<Wallet?> GetByUserAndCurrencyAsync(Guid userId, Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет наличие кошелька пользователя в указанной валюте.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="currency">Валюта кошелька.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><see langword="true" />, если кошелёк существует; иначе <see langword="false" />.</returns>
    Task<bool> ExistsAsync(Guid userId, Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет кошелёк в репозиторий.
    /// </summary>
    /// <param name="wallet">Кошелёк для добавления.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет накопленные изменения репозитория.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает упрощённые данные кошелька по его идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор кошелька.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Найденный кошелёк или <see langword="null" />, если кошелёк не существует.</returns>
    Task<Wallet?> GetByIdLightAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Загрузка отфильтрованной страницы транзакций кошелька.
    /// </summary>
    /// <param name="walletId"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <param name="type">Необязательный тип операции.</param>
    /// <param name="from">Начальная UTC-дата включительно.</param>
    /// <param name="to">Конечная UTC-дата включительно.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<PagedWalletTransactions> GetTransactionsPageAsync(
        Guid walletId,
        int page,
        int pageSize,
        WalletTransactionType? type = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Загрузка всех кошельков пользователя без дочерних коллекций. Используется для GET /wallets.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyCollection<Wallet>> ListByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
