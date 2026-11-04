using System.Reflection;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Otus.PaymentSystem.AspNetCore;
using Otus.PaymentSystem.AspNetCore.Middleware;
using Otus.PaymentSystem.Identity.Application.Commands.Register;
using Otus.PaymentSystem.Identity.Host.Contracts.Auth;
using Otus.PaymentSystem.Identity.Host.Contracts.Users;
using Otus.PaymentSystem.Identity.Host.Endpoints;
using Otus.PaymentSystem.Identity.Host.Middleware;
using Otus.PaymentSystem.Identity.Host.Seeding;
using Otus.PaymentSystem.Identity.Host.Swagger;
using Otus.PaymentSystem.Identity.Infrastructure;
using Otus.PaymentSystem.Identity.Infrastructure.Persistence;
using Otus.PaymentSystem.Identity.Infrastructure.Security;
using Serilog;
using Serilog.Formatting.Compact;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

// Swagger / OpenAPI: спецификация — контракт, поэтому её можно включить флагом
// в любом окружении (CI экспортирует её в Production-режиме, без БД и сидера).
var swaggerEnabled = builder.Configuration.GetValue("Swagger:Enabled", builder.Environment.IsDevelopment());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Otus Payment System — Identity API",
        Version = "v1",
        Description = "Регистрация, вход, профиль и роли. Публичный вход — через Gateway по путям /api/identity/**."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT access token: Authorization: Bearer {token}"
    });
    options.OperationFilter<SecurityRequirementOperationFilter>();

    // Публичный адрес — Gateway, а не сам Identity: клиент не должен знать внутренние порты.
    options.AddServer(new OpenApiServer
    {
        Url = "http://localhost:8080",
        Description = "Gateway (edge)"
    });
});

// Единый ProblemDetails: в Development detail содержит текст исключения.
builder.Services.AddApiProblems(options =>
    options.IncludeExceptionDetails = builder.Environment.IsDevelopment());

// MediatR: регистрируем все handlers из Application-сборки
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<RegisterCommand>());

// Infrastructure: EF Core PostgreSQL, PasswordHasher, JWT
builder.Services.AddIdentityInfrastructure(builder.Configuration);

// JWT bearer: валидация входящих токенов
IConfigurationSection jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
JwtOptions jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
IdentitySigningKey signingKeys = new(jwt, builder.Environment.EnvironmentName);
builder.Services.AddSingleton(signingKeys);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // сохраняем claim "sub" как есть
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKeys.PublicKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("role", "Admin"));
});

// Валидаторы запросов (FluentValidation)
builder.Services.AddSingleton<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddSingleton<IValidator<UpdateProfileRequest>, UpdateProfileRequestValidator>();
builder.Services.AddSingleton<IValidator<RefreshRequest>, RefreshRequestValidator>();
builder.Services.AddSingleton<IValidator<LogoutRequest>, LogoutRequestValidator>();

WebApplication app = builder.Build();

// Миграции на старте — только по явному флагу (docker compose).
// В обычном запуске схемой управляет `dotnet ef database update`:
// иначе несколько реплик начнут применять миграции конкурентно.
if (app.Configuration.GetValue("Database:AutoMigrate", false))
{
    using IServiceScope migrationScope = app.Services.CreateScope();
    await migrationScope.ServiceProvider
        .GetRequiredService<IdentityDbContext>()
        .Database.MigrateAsync();
}

// Порядок: correlation -> логи -> 500 -> пустые 4xx/5xx -> swagger -> доменные ошибки -> auth -> endpoints.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<UnhandledExceptionMiddleware>();
app.UseMiddleware<EmptyResponseProblemMiddleware>();

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// DomainException -> ProblemDetails (400/401/403/404/409)
app.UseMiddleware<DomainExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Health-check
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("HealthCheck");

// /auth/register, /auth/login, /auth/me
app.MapAuthEndpoints();

// Публичный ключ подписи: Gateway проверяет токены по нему, секрет больше не передаётся
app.MapGet("/.well-known/jwks.json", (IdentitySigningKey keys) => Results.Json(new { keys = new[] { keys.ToJwk() } }))
    .WithName("Jwks")
    .AllowAnonymous()
    .Produces(StatusCodes.Status200OK);

// /users/me, /users/{id}/block, /users/{id}/unblock
app.MapUsersEndpoints();

// Seed dev-админа: в Development создаём/повышаем пользователя из конфига (Seed:Admin).
if (app.Environment.IsDevelopment())
{
    using IServiceScope scope = app.Services.CreateScope();
    await DevAdminSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
