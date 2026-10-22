using Microsoft.EntityFrameworkCore;
using Otus.PaymentSystem.Identity.Domain.Aggregates;

namespace Otus.PaymentSystem.Identity.Infrastructure.Persistence;

/// <summary>EF Core контекст для Identity-контекста.</summary>
public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
