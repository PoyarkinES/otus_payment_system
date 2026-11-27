using Microsoft.EntityFrameworkCore;
using Otus.PaymentSystem.Wallets.Application.Contracts;
using Otus.PaymentSystem.Wallets.Application.Exceptions;
using Otus.PaymentSystem.Wallets.Application.Interfaces;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;

namespace Otus.PaymentSystem.Wallets.Infrastructure.Persistence;

public sealed class EfWalletRepository(WalletsDbContext db) : IWalletRepository
{
    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return db.Wallets
            .Include(w => w.Holds)
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<Wallet?> GetByHoldIdAsync(Guid holdId, CancellationToken cancellationToken = default)
    {
        return await db.Wallets.Include(w => w.Holds)
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.Holds.Any(h => h.Id == holdId), cancellationToken);
    }

    public async Task<Wallet?> GetByTransferIdAsync(Guid transferId, CancellationToken cancellationToken = default)
    {
        return await db.Wallets.Include(w => w.Holds)
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.Holds.Any(h => h.TransferId == transferId), cancellationToken);
    }

    public Task<Wallet?> GetByUserAndCurrencyAsync(Guid userId, Currency currency, CancellationToken cancellationToken = default)
    {
        return db.Wallets
            .Include(w => w.Holds)
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.UserId == userId && w.Currency == currency, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid userId, Currency currency, CancellationToken cancellationToken = default)
    {
        return db.Wallets.AnyAsync(w => w.UserId == userId && w.Currency == currency, cancellationToken);
    }

    public async Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        await db.Wallets.AddAsync(wallet, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();

            throw new WalletConcurrencyException(
                "Кошелёк был изменён другим запросом. Повторите операцию.");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException
            {
                SqlState: "23505",
                ConstraintName: "UX_WalletTransactions_WalletId_ReferenceId"
            })
        {
            db.ChangeTracker.Clear();
            throw new DuplicateWalletOperationException();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException
            {
                SqlState: "23505",
                ConstraintName: "IX_Holds_TransferId"
            })
        {
            db.ChangeTracker.Clear();
            throw new DuplicateHoldException();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException
            {
                SqlState: "23505",
                ConstraintName: "IX_Wallets_UserId_Currency"
            })
        {
            db.ChangeTracker.Clear();
            throw new DuplicateWalletException();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
            throw new DomainException("Нарушено уникальное ограничение.", DomainError.Conflict);
        }
    }

    public async Task<Wallet?> GetByIdLightAsync(Guid id, CancellationToken cancellationToken = default)
    {
       return await db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<PagedWalletTransactions> GetTransactionsPageAsync(
        Guid walletId,
        int page,
        int pageSize,
        WalletTransactionType? type = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var skip = (page - 1) * pageSize;
        IQueryable<WalletTransaction> query = db.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == walletId);

        if (type.HasValue)
        {
            query = query.Where(t => t.Type == type.Value);
        }

        if (from.HasValue)
        {
            DateTimeOffset fromUtc = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(t => t.CreatedAt >= fromUtc);
        }

        if (to.HasValue && to.Value < DateOnly.MaxValue)
        {
            DateTimeOffset toUtcExclusive = to.Value
                .AddDays(1)
                .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(t => t.CreatedAt < toUtcExclusive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        List<WalletTransaction> transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedWalletTransactions(transactions, totalCount);
    }

    public async Task<IReadOnlyCollection<Wallet>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await db.Wallets
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.Currency)
            .ToListAsync(cancellationToken);
    }
}
