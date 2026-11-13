using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Otus.PaymentSystem.Wallets.Application.Interfaces;
using Otus.PaymentSystem.Wallets.Infrastructure.Persistence;

namespace Otus.PaymentSystem.Wallets.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWalletsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WalletsDb")
            ?? throw new InvalidOperationException("ConnectionStrings:WalletsDb is not configured.");

        services.AddDbContext<WalletsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IWalletRepository, EfWalletRepository>();

        return services;
    }
}
