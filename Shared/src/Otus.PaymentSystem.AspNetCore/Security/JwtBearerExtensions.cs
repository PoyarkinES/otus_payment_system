using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;

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

        services.AddSingleton(serviceProvider => new JwksKeyStore(
            new HttpClient { Timeout = TimeSpan.FromSeconds(5) },
            jwksUri,
            serviceProvider.GetRequiredService<ILogger<JwksKeyStore>>()));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwksKeyStore>((options, keyStore) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = (_, _, _, _) => keyStore.Keys,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidAlgorithms = ["RS256"]
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = async context =>
                    {
                        const string bearerPrefix = "Bearer ";
                        string authorization = context.Request.Headers.Authorization.ToString();
                        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
                            return;

                        string token = authorization[bearerPrefix.Length..].Trim();
                        var handler = new JwtSecurityTokenHandler();
                        if (!handler.CanReadToken(token))
                            return;

                        try
                        {
                            string? kid = handler.ReadJwtToken(token).Header.Kid;
                            await keyStore.RefreshIfNeededAsync(kid, context.HttpContext.RequestAborted);
                        }
                        catch (ArgumentException)
                        {
                        }
                        catch (SecurityTokenException)
                        {
                        }
                    }
                };
            });

        return services;
    }
}
