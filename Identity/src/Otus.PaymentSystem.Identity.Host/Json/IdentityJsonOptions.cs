using System.Text.Json;
using System.Text.Json.Serialization;

namespace Otus.PaymentSystem.Identity.Host.Json;

/// <summary>
/// Единые JSON-настройки Identity Host.
/// Enum'ы уходят строками: контракт должен читаться человеком и клиентом
/// ("Active", а не 0) — см. #108.
/// </summary>
public static class IdentityJsonOptions
{
    public static void Apply(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
    }
}
