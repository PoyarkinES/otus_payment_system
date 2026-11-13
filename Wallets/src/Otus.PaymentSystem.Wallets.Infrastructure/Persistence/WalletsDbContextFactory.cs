using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Otus.PaymentSystem.Wallets.Infrastructure.Persistence;

public sealed class WalletsDbContextFactory : IDesignTimeDbContextFactory<WalletsDbContext>
{
    public WalletsDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("WALLETS_CONNECTION_STRING")
                 ?? "Host=localhost;Port=5433;Database=otus_wallets;Username=otus;Password=payments";

        var options = new DbContextOptionsBuilder<WalletsDbContext>()
            .UseNpgsql(cs)
            .Options;

        return new WalletsDbContext(options);
    }
}
