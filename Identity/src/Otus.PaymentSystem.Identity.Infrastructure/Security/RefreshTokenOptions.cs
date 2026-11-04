namespace Otus.PaymentSystem.Identity.Infrastructure.Security;

/// <summary>Настройки выпуска refresh-токенов (секция "RefreshTokens" в appsettings).</summary>
public class RefreshTokenOptions
{
    public const string SectionName = "RefreshTokens";

    /// <summary>Срок жизни refresh-токена в днях.</summary>
    public int LifetimeDays { get; init; } = 30;

    /// <summary>Длина случайной части в байтах: 32 байта = 256 бит энтропии.</summary>
    public int SizeBytes { get; init; } = 32;
}
