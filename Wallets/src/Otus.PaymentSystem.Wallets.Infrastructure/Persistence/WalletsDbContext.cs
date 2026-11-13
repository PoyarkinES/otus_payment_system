using Microsoft.EntityFrameworkCore;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;

namespace Otus.PaymentSystem.Wallets.Infrastructure.Persistence;

public sealed class WalletsDbContext : DbContext
{
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Hold> Holds => Set<Hold>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Outbox> Outbox => Set<Outbox>();

    public WalletsDbContext(DbContextOptions<WalletsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WalletsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
