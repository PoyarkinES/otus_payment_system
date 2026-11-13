using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;

namespace Otus.PaymentSystem.Wallets.Infrastructure.Persistence.Configurations;

public sealed class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("WalletTransactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.WalletId)
            .IsRequired();

        builder.Property(t => t.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(t => t.Currency)
            .HasConversion(
                c => c.Value,
                v => Currency.Create(v))
            .HasColumnName("Currency")
            .HasColumnType("varchar(3)")
            .IsRequired();

        builder.Property(t => t.BalanceBefore)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(t => t.BalanceAfter)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(t => t.ReferenceId)
            .IsRequired(false);

        builder.Property(t => t.Description)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(t => t.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(t => new { t.WalletId, t.CreatedAt })
            .HasDatabaseName("IX_WalletTransactions_WalletId_CreatedAt");
    }
}
