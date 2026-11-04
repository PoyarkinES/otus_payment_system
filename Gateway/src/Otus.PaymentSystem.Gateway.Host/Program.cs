using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Otus.PaymentSystem.AspNetCore;
using Otus.PaymentSystem.AspNetCore.Middleware;
using Otus.PaymentSystem.Gateway.Host.Endpoints;
using Otus.PaymentSystem.Gateway.Host.Middleware;
using Otus.PaymentSystem.Gateway.Host.Security;
using Serilog;
using Serilog.Formatting.Compact;
using System;
using System.Text;
using System.Threading.RateLimiting;
using Otus.PaymentSystem.AspNetCore.Problems;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

// Единый ProblemDetails: в Development detail содержит текст исключения.
builder.Services.AddApiProblems(options =>
    options.IncludeExceptionDetails = builder.Environment.IsDevelopment());

JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                                               ?? throw new InvalidOperationException("Секция Jwt не настроена.");

if (string.IsNullOrWhiteSpace(jwt.JwksUri))
{
    throw new InvalidOperationException(
        "Jwt:JwksUri не настроен. Gateway проверяет подпись по публичному ключу Identity.");
}

JwksKeyStore keyStore = new(new HttpClient { Timeout = TimeSpan.FromSeconds(5) }, jwt.JwksUri);
builder.Services.AddSingleton(keyStore);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Gateway проверяет токен локально и не ходит в Identity на каждый запрос.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (_, _, kid, _) =>
            {
                IList<SecurityKey> keys = keyStore.Keys;

                // Незнакомый kid = Identity перезапустился или ключ ротировали: обновляем кэш сразу,
                // иначе новые токены будут получать 401 до истечения TTL.
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser());
});

// Rate limiting на краю: по ADR 0001 он живёт в Gateway, а не в сервисах.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Перебор паролей: 5 попыток в минуту с одного IP.
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // Долбёжка refresh: 30 в минуту с одного IP.
    options.AddPolicy("refresh", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // BFF-группа: /bff/health анонимный и на каждый запрос ходит в Identity.
    // 60 в минуту на IP: healthcheck контейнера — 6 в минуту, запас десятикратный.
    options.AddPolicy("bff", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    options.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

        await ApiProblems.WriteAsync(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            "Слишком много попыток. Повторите позже.");
    };
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                         ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName));
});

// BFF: единственное место, где Gateway сам ходит в сервисы, — health-агрегат.
// Адрес Identity берём из кластера прокси, чтобы health не мог разойтись с маршрутизацией.
string identityAddress = builder.Configuration["ReverseProxy:Clusters:identity:Destinations:primary:Address"]
                         ?? "http://localhost:5100/";

builder.Services.AddHttpClient(BffEndpoints.IdentityClientName, client =>
{
    client.BaseAddress = new Uri(identityAddress, UriKind.Absolute);
    // Health не должен ждать: если Identity тормозит, ответ "degraded" полезнее, чем таймаут клиента.
    client.Timeout = TimeSpan.FromSeconds(3);
});

WebApplication app = builder.Build();

// Порядок: correlation -> логи -> 500 -> пустые 4xx/5xx -> CORS -> auth -> X-User-Id -> authorization.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<UnhandledExceptionMiddleware>();
app.UseMiddleware<EmptyResponseProblemMiddleware>();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseMiddleware<UserIdHeaderMiddleware>();
app.UseAuthorization();

// Health-check самого Gateway. Агрегированный health и /bff/me — в BffEndpoints (#49).
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "gateway"
})).AllowAnonymous();

app.MapBffEndpoints();

app.UseRateLimiter();
app.MapReverseProxy();

if (!await app.Services.GetRequiredService<JwksKeyStore>().RefreshAsync())
    Log.Warning("JWKS недоступен при старте Gateway: {Uri}", jwt.JwksUri);

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Нужен для интеграционных тестов через WebApplicationFactory.</summary>
public partial class Program
{
    
}
