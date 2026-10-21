using Microsoft.EntityFrameworkCore;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Infrastructure.Persistence.Repositories;

/// <summary>EF Core реализация <see cref="IUserRepository"/>.</summary>
public class EfUserRepository(IdentityDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await db.Users.AddAsync(user, ct);
}
