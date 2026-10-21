using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Host.Seeding;

/// <summary>
/// Создаёт dev-администратора из конфига (Seed:Admin:Email / Seed:Admin:Password).
/// Идемпотентно: если пользователь с таким email уже есть — повышает до Admin.
/// Только для Development; в prod-сценарии роль выдаётся вручную/через миграцию.
/// </summary>
public static class DevAdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["Seed:Admin:Email"];
        var password = configuration["Seed:Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var users = services.GetRequiredService<IUserRepository>();
        var hasher = services.GetRequiredService<IPasswordHasher>();

        var existing = await users.GetByEmailAsync(new Email(email));
        if (existing is not null)
        {
            if (existing.Role != UserRole.Admin)
            {
                existing.PromoteToAdmin();
                await users.SaveChangesAsync();
            }
            return;
        }

        var admin = User.Register(new Email(email), hasher.Hash(password));
        admin.PromoteToAdmin();
        await users.AddAsync(admin);
        await users.SaveChangesAsync();
    }
}
