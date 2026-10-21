using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Otus.PaymentSystem.Identity.Host.Security;

namespace Otus.PaymentSystem.Identity.Host.Tests;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_ReadsSubClaim()
    {
        var id = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, id.ToString())
        }));

        Assert.Equal(id, principal.GetUserId());
    }

    [Fact]
    public void GetUserId_UnknownClaim_ReturnsNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.Null(principal.GetUserId());
    }

    [Fact]
    public void GetUserId_InvalidGuid_ReturnsNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "not-a-guid")
        }));

        Assert.Null(principal.GetUserId());
    }

    [Fact]
    public void GetEmail_ReadsEmailClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Email, "user@example.com")
        }));

        Assert.Equal("user@example.com", principal.GetEmail());
    }
}
