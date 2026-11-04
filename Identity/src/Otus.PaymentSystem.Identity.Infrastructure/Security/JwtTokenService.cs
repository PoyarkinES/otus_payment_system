using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;

namespace Otus.PaymentSystem.Identity.Infrastructure.Security;

/// <summary>
/// Реализация <see cref="ITokenService"/>: JWT с подписью RS256.
/// Claims: sub, email, jti, role, exp. Ключ — <see cref="IdentitySigningKey"/>.
/// </summary>
public class JwtTokenService(IOptions<JwtOptions> options, IdentitySigningKey signingKey) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public TokenResult GenerateToken(User user)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = now.AddMinutes(_options.LifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("role", user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey.PrivateKey, SecurityAlgorithms.RsaSha256));

        return new TokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
