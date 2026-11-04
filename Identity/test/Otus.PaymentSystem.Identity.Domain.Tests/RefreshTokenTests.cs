using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Errors;
using Otus.PaymentSystem.Identity.Domain.Exceptions;

namespace Otus.PaymentSystem.Identity.Domain.Tests;

public class RefreshTokenTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static RefreshToken NewToken(int days = 30) =>
        RefreshToken.Issues(UserId, "hash", DateTimeOffset.UtcNow.AddDays(days));

    [Fact]
    public void Issues_WithEmptyHash_Throws()
    {
        Assert.Throws<DomainException>(
            () => RefreshToken.Issues(UserId, " ", DateTimeOffset.UtcNow.AddDays(1)));
    }

    [Fact]
    public void Issues_WithPastExpiry_Throws()
    {
        Assert.Throws<DomainException>(
            () => RefreshToken.Issues(UserId, "hash", DateTimeOffset.UtcNow.AddSeconds(-1)));
    }

    [Fact]
    public void EnsureActive_AfterExpiry_ThrowsUnauthorized()
    {
        var token = NewToken();

        var ex = Assert.Throws<DomainException>(
            () => token.EnsureActive(DateTimeOffset.UtcNow.AddDays(31)));

        Assert.Equal(DomainError.Unauthorized, ex.Error);
    }

    [Fact]
    public void Revoke_IsIdempotent()
    {
        var token = NewToken();

        token.Revoke();
        DateTimeOffset? firstRevokedAt = token.RevokedAt;
        token.Revoke(Guid.NewGuid());

        Assert.Equal(firstRevokedAt, token.RevokedAt);
        Assert.Null(token.ReplacedByTokenId);
    }

    [Fact]
    public void WasRotated_TrueOnlyWithSuccessor()
    {
        var plain = NewToken();
        Assert.False(plain.WasRotated);

        plain.Revoke();                       // как при logout — преемника нет
        Assert.False(plain.WasRotated);

        var rotated = NewToken();
        rotated.Revoke(Guid.NewGuid());       // как при ротации
        Assert.True(rotated.WasRotated);
    }
}
