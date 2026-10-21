using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;
using Otus.PaymentSystem.Identity.Infrastructure.Security;

namespace Otus.PaymentSystem.Identity.Infrastructure.Tests;

public class JwtTokenServiceTests
{
    private const string Secret = "super-secret-key-for-tests-32chars-min";
    private const string Issuer = "otus-payment-system";
    private const string Audience = "otus-payment-system-clients";

    private static JwtTokenService CreateService(int lifetimeMinutes = 60)
    {
        var options = Options.Create(new JwtOptions
        {
            Secret = Secret,
            Issuer = Issuer,
            Audience = Audience,
            LifetimeMinutes = lifetimeMinutes
        });
        return new JwtTokenService(options);
    }

    private static User CreateUser()
        => User.Register(new Email("user@example.com"), "hash-1");

    private static JwtSecurityToken ReadToken(string token)
        => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void GenerateToken_ReturnsNonEmptyJwt()
    {
        var service = CreateService();
        var user = CreateUser();

        var result = service.GenerateToken(user);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal(3, result.AccessToken.Split('.').Length); // header.payload.signature
    }

    [Fact]
    public void GenerateToken_ContainsExpectedClaims()
    {
        var service = CreateService();
        var user = CreateUser();

        var token = ReadToken(service.GenerateToken(user).AccessToken);

        Assert.Equal(user.Id.ToString(), token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("user@example.com", token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.False(string.IsNullOrWhiteSpace(token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value));
        Assert.Equal("User", token.Claims.Single(c => c.Type == "role").Value);
    }

    [Fact]
    public void GenerateToken_ExpiresAt_MatchesTokenExpiry()
    {
        var service = CreateService(lifetimeMinutes: 30);
        var user = CreateUser();

        var before = DateTime.UtcNow;
        var result = service.GenerateToken(user);
        var token = ReadToken(result.AccessToken);
        var after = DateTime.UtcNow;

        // exp = now + 30 min (с точностью до секунды)
        Assert.True(token.ValidTo >= before.AddMinutes(30).AddSeconds(-1));
        Assert.True(token.ValidTo <= after.AddMinutes(30).AddSeconds(1));
        Assert.True(result.ExpiresAt.UtcDateTime >= before.AddMinutes(30).AddSeconds(-1));
    }

    [Fact]
    public void GenerateToken_IsValidAgainstSecretAndIssuer()
    {
        var service = CreateService();
        var user = CreateUser();
        var token = service.GenerateToken(user).AccessToken;

        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // MapInboundClaims = false: иначе handler переименует "sub" в ClaimTypes.NameIdentifier.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, validation, out _);

        Assert.NotNull(principal);
        Assert.Equal(user.Id.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    }
}
