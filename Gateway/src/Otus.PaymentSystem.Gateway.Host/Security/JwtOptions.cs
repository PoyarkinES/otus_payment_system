namespace Otus.PaymentSystem.Gateway.Host.Security;

/// <summary>Настройки валидации JWT на Gateway (секция "Jwt").</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
}
