namespace Otus.PaymentSystem.Wallets.Host;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = null!;
    public string Audience { get; init; } = null!;
    public string JwksUri { get; init; } = null!;
}
