using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;

namespace Otus.PaymentSystem.Wallets.Infrastructure.Persistence.Configurations;

public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .ValueGeneratedNever();

        builder.Property(w => w.UserId)
            .IsRequired();

        builder.Property(w => w.Currency)
            .HasConversion(
                c => c.Value,
                v => Currency.Create(v))
            .HasColumnName("Currency")
            .HasColumnType("varchar(3)")
            .IsRequired();

        builder.Property(w => w.Available)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(w => w.Held)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(w => w.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(w => w.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(w => w.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(w => w.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Ignore(w => w.Total);
        builder.Ignore(w => w.DomainEvents);

        builder.HasIndex(w => new { w.UserId, w.Currency })
            .IsUnique()
            .HasDatabaseName("IX_Wallets_UserId_Currency");

        builder.HasMany(w => w.Holds)
            .WithOne()
            .HasForeignKey(h => h.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(w => w.Transactions)
            .WithOne()
            .HasForeignKey(t => t.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(w => w.Holds)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(w => w.Transactions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
