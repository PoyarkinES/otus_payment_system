using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;

namespace Otus.PaymentSystem.Wallets.Infrastructure.Persistence.Configurations;

public sealed class HoldConfiguration : IEntityTypeConfiguration<Hold>
{
    public void Configure(EntityTypeBuilder<Hold> builder)
    {
        builder.ToTable("Holds");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .ValueGeneratedNever();

        builder.Property(h => h.WalletId)
            .IsRequired();

        builder.Property(h => h.TransferId)
            .IsRequired();

        builder.Property(h => h.UserId)
            .IsRequired();

        builder.Property(h => h.Amount)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(h => h.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(h => h.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(h => h.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(h => h.TransferId)
            .IsUnique()
            .HasDatabaseName("IX_Holds_TransferId");
    }
}
