using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Otus.PaymentSystem.Identity.Infrastructure.Security;

/// <summary>
/// Источник ключа подписи access-токенов. Ключ либо приходит из конфигурации,
/// либо (только в Development) генерируется эфемерно на время жизни процесса.
/// </summary>
public sealed class IdentitySigningKey
{
    public RsaSecurityKey PrivateKey { get; }
    public RsaSecurityKey PublicKey { get; }
    public string KeyId { get; }

    public IdentitySigningKey(JwtOptions options, string environmentName)
    {
        RSA rsa;

        if (!string.IsNullOrWhiteSpace(options.PrivateKeyPem))
        {
            rsa = RSA.Create();
            rsa.ImportFromPem(options.PrivateKeyPem);
        }
        else if (environmentName == "Development")
        {
            rsa = RSA.Create(2048);   // эфемерный: токены переживут только этот процесс
        }
        else
        {
            throw new InvalidOperationException(
                "Jwt:PrivateKeyPem не настроен: в не-Development окружении подпись RS256 без ключа невозможна.");
        }

        var keyIdBytes = SHA256.HashData(rsa.ExportRSAPublicKey());
        KeyId = Convert.ToHexString(keyIdBytes)[..16];

        PrivateKey = new RsaSecurityKey(rsa) { KeyId = KeyId };
        PublicKey = new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false)) { KeyId = KeyId };
    }

    /// <summary>Публичная часть в формате JWK для /.well-known/jwks.json.</summary>
    public object ToJwk()
    {
        RSAParameters p = PublicKey.Parameters;   // ключ создан из RSAParameters
        return new
        {
            kty = "RSA",
            use = "sig",
            alg = "RS256",
            kid = KeyId,
            n = Base64UrlEncoder.Encode(p.Modulus),
            e = Base64UrlEncoder.Encode(p.Exponent)
        };
    }
}
