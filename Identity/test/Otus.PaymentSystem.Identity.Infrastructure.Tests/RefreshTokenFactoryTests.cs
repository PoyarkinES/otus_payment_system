using Microsoft.Extensions.Options;
using Otus.PaymentSystem.Identity.Infrastructure.Security;

namespace Otus.PaymentSystem.Identity.Infrastructure.Tests;

public class RefreshTokenFactoryTests
{
    private static RefreshTokenFactory CreateSut(int days = 30, int size = 32) =>
        new(Options.Create(new RefreshTokenOptions { LifetimeDays = days, SizeBytes = size }));

    [Fact]
    public void Issue_ReturnsUrlSafeTokenAndMatchingHash()
    {
        var sut = CreateSut();

        var issued = sut.Issue();

        Assert.Equal(43, issued.Raw.Length);              // 32 байта в base64url без padding
        Assert.DoesNotContain('+', issued.Raw);
        Assert.DoesNotContain('/', issued.Raw);
        Assert.DoesNotContain('=', issued.Raw);
        Assert.Equal(64, issued.Hash.Length);             // SHA-256 в hex
        Assert.Equal(issued.Hash, sut.Hash(issued.Raw));  // хэш от строки — тот же при поиске
    }

    [Fact]
    public void Issue_UsesConfiguredLifetime()
    {
        var issued = CreateSut(days: 7).Issue();

        Assert.InRange(
            issued.ExpiresAt,
            DateTimeOffset.UtcNow.AddDays(6.9),
            DateTimeOffset.UtcNow.AddDays(7.1));
    }
}
