using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Otus.PaymentSystem.Gateway.Host.Swagger;

/// <summary>
/// Добавляет требование Bearer защищённым операциям.
/// Копия фильтра из Identity: вынести в Shared можно, но тогда Swashbuckle
/// потянется во все сервисы, включая те, где спеки ещё нет.
/// </summary>
public sealed class SecurityRequirementOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        bool requiresAuth = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Any();

        if (!requiresAuth)
            return;

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = []
            }
        ];
    }
}
