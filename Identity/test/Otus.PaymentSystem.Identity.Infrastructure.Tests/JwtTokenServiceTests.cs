using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;
using Otus.PaymentSystem.Identity.Infrastructure.Security;

namespace Otus.PaymentSystem.Identity.Infrastructure.Tests;

public class JwtTokenServiceTests
{
    private const string Issuer = "otus-payment-system";
    private const string Audience = "otus-payment-system-clients";

    private static (JwtTokenService Service, IdentitySigningKey Keys) CreateService(int lifetimeMinutes = 60)
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            LifetimeMinutes = lifetimeMinutes
        });

        // Пустой PrivateKeyPem + Development → эфемерный ключ на время теста
        var keys = new IdentitySigningKey(options.Value, "Development");
        return (new JwtTokenService(options, keys), keys);
    }

    private static User CreateUser()
        => User.Register(new Email("user@example.com"), "hash-1");

    private static JwtSecurityToken ReadToken(string token)
        => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void GenerateToken_ReturnsNonEmptyJwt()
    {
        var (service, _) = CreateService();
        var user = CreateUser();

        var result = service.GenerateToken(user);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal(3, result.AccessToken.Split('.').Length);
    }

    [Fact]
    public void GenerateToken_ContainsExpectedClaims()
    {
        var (service, _) = CreateService();
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
        var (service, _) = CreateService(lifetimeMinutes: 30);
        var user = CreateUser();

        var before = DateTime.UtcNow;
        var result = service.GenerateToken(user);
        var token = ReadToken(result.AccessToken);
        var after = DateTime.UtcNow;

        Assert.True(token.ValidTo >= before.AddMinutes(30).AddSeconds(-1));
        Assert.True(token.ValidTo <= after.AddMinutes(30).AddSeconds(1));
        Assert.True(result.ExpiresAt.UtcDateTime >= before.AddMinutes(30).AddSeconds(-1));
    }

    [Fact]
    public void GenerateToken_HasRs256HeaderWithKeyId()
    {
        var (service, keys) = CreateService();

        var token = ReadToken(service.GenerateToken(CreateUser()).AccessToken);

        Assert.Equal("RS256", token.Header.Alg);
        Assert.Equal(keys.KeyId, token.Header.Kid);
    }

    [Fact]
    public void GenerateToken_IsValidAgainstPublicKey()
    {
        var (service, keys) = CreateService();
        var user = CreateUser();
        var token = service.GenerateToken(user).AccessToken;

        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = keys.PublicKey,
            ValidateLifetime = true,
            ValidAlgorithms = ["RS256"],
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, validation, out _);

        Assert.Equal(user.Id.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    }
}
