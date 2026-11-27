using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Otus.PaymentSystem.Gateway.Host.Tests;

public sealed class JwtValidationFailureTests
{
    [Fact]
    public async Task ProtectedEndpoint_WhenJwksIsUnavailable_ReturnsUnauthorized()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Jwt:Issuer", "test");
                builder.UseSetting("Jwt:Audience", "test");
                builder.UseSetting("Jwt:JwksUri", "http://127.0.0.1:1/.well-known/jwks.json");
            });
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/wallets");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateSignedToken());

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    private static string CreateSignedToken()
    {
        using RSA rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "rotated-key" };
        var token = new JwtSecurityToken(
            issuer: "test",
            audience: "test",
            claims: [new Claim("sub", "test-user")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}