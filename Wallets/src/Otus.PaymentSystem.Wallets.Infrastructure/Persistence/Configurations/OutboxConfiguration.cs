using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;

namespace Otus.PaymentSystem.Wallets.Infrastructure.Persistence.Configurations;

public sealed class OutboxConfiguration : IEntityTypeConfiguration<Outbox>
{
    public void Configure(EntityTypeBuilder<Outbox> builder)
    {
        builder.ToTable("Outbox");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .ValueGeneratedNever();

        builder.Property(o => o.EventType)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(o => o.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(o => o.OccurredAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(o => o.PublishedAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);
    }
}
