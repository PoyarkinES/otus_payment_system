using Microsoft.IdentityModel.Tokens;

namespace Otus.PaymentSystem.Gateway.Host.Security;

/// <summary>
/// Кэш публичных ключей Identity (JWKS). Gateway проверяет подпись локально,
/// ключи обновляются раз в 10 минут.
/// </summary>
public sealed class JwksKeyStore(HttpClient http, string jwksUri)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private IList<SecurityKey> _keys = Array.Empty<SecurityKey>();
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;

    public IList<SecurityKey> Keys
    {
        get
        {
            if (DateTimeOffset.UtcNow - _loadedAt > Ttl)
                RefreshAsync().GetAwaiter().GetResult();

            return _keys;
        }
    }

    public async Task<bool> RefreshAsync()
    {
        try
        {
            string json = await http.GetStringAsync(jwksUri);
            _keys = new JsonWebKeySet(json).GetSigningKeys().ToList();
            _loadedAt = DateTimeOffset.UtcNow;
            return _keys.Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
