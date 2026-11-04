using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Otus.PaymentSystem.Identity.Application.Interfaces;

namespace Otus.PaymentSystem.Identity.Infrastructure.Security;

/// <summary>Выпуск refresh-токенов: случайные байты, их SHA-256 и срок из настроек.</summary>
public class RefreshTokenFactory(IOptions<RefreshTokenOptions> options) : IRefreshTokenFactory
{
    private readonly RefreshTokenOptions _options = options.Value;

    /// <inheritdoc />
    public IssuedRefreshToken Issue()
    {
        string raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(_options.SizeBytes));
        return new IssuedRefreshToken(raw, Hash(raw), DateTimeOffset.UtcNow.AddDays(_options.LifetimeDays));
    }

    /// <inheritdoc />
    public string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
