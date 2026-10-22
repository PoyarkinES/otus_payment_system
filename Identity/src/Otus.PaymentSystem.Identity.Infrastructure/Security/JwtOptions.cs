namespace Otus.PaymentSystem.Identity.Infrastructure.Security;

/// <summary>Настройки выпуска JWT (секция "Jwt" в appsettings).</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int LifetimeMinutes { get; init; } = 60;
}
