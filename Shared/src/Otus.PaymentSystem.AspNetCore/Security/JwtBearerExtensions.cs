using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Otus.PaymentSystem.AspNetCore.Security;

public static class JwtBearerExtensions
{
    public static IServiceCollection AddJwtBearerFromJwks(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Jwt")
    {
        var section = configuration.GetSection(sectionName);
        var issuer = section["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = section["Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
        var jwksUri = section["JwksUri"] ?? throw new InvalidOperationException("Jwt:JwksUri is not configured.");

        var keyStore = new JwksKeyStore(new HttpClient { Timeout = TimeSpan.FromSeconds(5) }, jwksUri);
        services.AddSingleton(keyStore);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = (_, _, kid, _) =>
                    {
                        IList<SecurityKey> keys = keyStore.Keys;
                        if (kid is not null && keys.All(k => k.KeyId != kid))
                        {
                            keyStore.RefreshAsync().GetAwaiter().GetResult();
                            keys = keyStore.Keys;
                        }
                        return keys;
                    },
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidAlgorithms = ["RS256"]
                };
            });

        return services;
    }
}
