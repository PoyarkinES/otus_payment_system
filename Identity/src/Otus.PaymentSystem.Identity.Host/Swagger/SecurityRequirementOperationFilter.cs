using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Otus.PaymentSystem.Identity.Host.Swagger;

/// <summary>
/// Добавляет требование Bearer защищённым операциям.
/// Документное требование не используется: пустой массив security нельзя сериализовать
/// через Microsoft.OpenApi, а отсутствие свойства означает «наследовать документное».
/// </summary>
public sealed class SecurityRequirementOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Только явно защищённые операции: наличие Authorize, а не отсутствие AllowAnonymous.
        // Иначе в спеке «под замком» окажется и открытый /health.
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
