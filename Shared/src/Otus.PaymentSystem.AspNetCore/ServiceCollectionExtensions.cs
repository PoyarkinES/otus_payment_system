using Microsoft.Extensions.DependencyInjection;
using Otus.PaymentSystem.AspNetCore.Problems;

namespace Otus.PaymentSystem.AspNetCore;

/// <summary>Регистрация общих web-примитивов в Host-проектах.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Добавляет настройки единого ProblemDetails.</summary>
    public static IServiceCollection AddApiProblems(
        this IServiceCollection services,
        Action<ApiProblemOptions>? configure = null)
    {
        services.AddOptions<ApiProblemOptions>();

        if (configure is not null)
            services.Configure(configure);

        return services;
    }
}
