using Microsoft.EntityFrameworkCore;
using Otus.PaymentSystem.Wallets.Application.Interfaces;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;

namespace Otus.PaymentSystem.Wallets.Infrastructure.Persistence;

public sealed class EfWalletRepository : IWalletRepository
{
    private readonly WalletsDbContext _db;

    public EfWalletRepository(WalletsDbContext db) => _db = db;

    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Wallets
            .Include(w => w.Holds)
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public Task<Wallet?> GetByUserAndCurrencyAsync(Guid userId, Currency currency, CancellationToken cancellationToken = default)
        => _db.Wallets
            .Include(w => w.Holds)
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.UserId == userId && w.Currency == currency, cancellationToken);

    public Task<bool> ExistsAsync(Guid userId, Currency currency, CancellationToken cancellationToken = default)
        => _db.Wallets.AnyAsync(w => w.UserId == userId && w.Currency == currency, cancellationToken);

    public async Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
        => await _db.Wallets.AddAsync(wallet, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new DomainException("Нарушено уникальное ограничение.", DomainError.Conflict);
        }
    }
}
