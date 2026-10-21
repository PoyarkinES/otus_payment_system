using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Infrastructure.Persistence;
using Otus.PaymentSystem.Identity.Infrastructure.Persistence.Repositories;
using Otus.PaymentSystem.Identity.Infrastructure.Security;

namespace Otus.PaymentSystem.Identity.Infrastructure;

/// <summary>Регистрация сервисов Identity Infrastructure в DI.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseInMemoryDatabase("identity"));

        services.AddScoped<IUserRepository, EfUserRepository>();

        services.AddSingleton<PasswordHasher<object>>();
        services.AddScoped<IPasswordHasher, AspNetPasswordHasher>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }
}
