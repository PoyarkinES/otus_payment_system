using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Domain.Tests;

public class EmailTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("USER@EXAMPLE.COM")]
    [InlineData("  user@example.com  ")]
    [InlineData("a.b+c@sub.domain.co.uk")]
    public void Constructor_ValidEmail_NormalizesToLowercaseTrimmed(string input)
    {
        var email = new Email(input);

        Assert.Equal(input.Trim().ToLowerInvariant(), email.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("plainaddress")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user@@example.com")]
    [InlineData("user@example")]
    [InlineData("user @example.com")]
    public void Constructor_InvalidEmail_ThrowsDomainException(string? input)
    {
        Assert.Throws<DomainException>(() => new Email(input!));
    }

    [Fact]
    public void Equality_SameNormalizedValue_IsEqual()
    {
        var a = new Email("User@Example.com");
        var b = new Email("user@example.com");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var email = new Email("user@example.com");

        Assert.Equal("user@example.com", email.ToString());
    }
}
