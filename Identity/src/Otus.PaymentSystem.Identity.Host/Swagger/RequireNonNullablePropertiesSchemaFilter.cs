using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Otus.PaymentSystem.Identity.Host.Swagger;

/// <summary>
/// Помечает non-nullable свойства как required в OpenAPI-спеке.
/// Без этого фильтра Swashbuckle не заполняет массив required, даже если
/// SupportNonNullableReferenceTypes включён — все reference-типы остаются
/// опциональными, что ломает строгую типизацию сгенерированного клиента.
/// </summary>
public sealed class RequireNonNullablePropertiesSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties is null)
            return;

        // Non-nullable свойства — обязательные: Swashbuckle с SupportNonNullableReferenceTypes
        // ставит Nullable=false для string, но не добавляет их в Required.
        foreach (var property in schema.Properties.Where(p => !p.Value.Nullable))
        {
            if (!schema.Required.Contains(property.Key))
                schema.Required.Add(property.Key);
        }
    }
}
