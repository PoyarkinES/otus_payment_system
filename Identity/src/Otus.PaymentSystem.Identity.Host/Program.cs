using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Otus.PaymentSystem.AspNetCore;
using Otus.PaymentSystem.AspNetCore.Middleware;
using Otus.PaymentSystem.Identity.Application.Commands.Register;
using Otus.PaymentSystem.Identity.Host.Contracts.Auth;
using Otus.PaymentSystem.Identity.Host.Contracts.Users;
using Otus.PaymentSystem.Identity.Host.Endpoints;
using Otus.PaymentSystem.Identity.Host.Middleware;
using Otus.PaymentSystem.Identity.Host.Seeding;
using Otus.PaymentSystem.Identity.Infrastructure;
using Otus.PaymentSystem.Identity.Infrastructure.Security;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Единый ProblemDetails: в Development detail содержит текст исключения.
builder.Services.AddApiProblems(options =>
    options.IncludeExceptionDetails = builder.Environment.IsDevelopment());

// MediatR: регистрируем все handlers из Application-сборки
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<RegisterCommand>());

// Infrastructure: EF Core PostgreSQL, PasswordHasher, JWT
builder.Services.AddIdentityInfrastructure(builder.Configuration);

// JWT bearer: валидация входящих токенов
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("role", "Admin"));
});

// Валидаторы запросов (FluentValidation)
builder.Services.AddSingleton<RegisterRequestValidator>();
builder.Services.AddSingleton<LoginRequestValidator>();
builder.Services.AddSingleton<UpdateProfileRequestValidator>();

var app = builder.Build();

// Порядок: correlation -> логи -> 500 -> пустые 4xx/5xx -> доменные ошибки -> auth -> endpoints.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<UnhandledExceptionMiddleware>();
app.UseMiddleware<EmptyResponseProblemMiddleware>();

// Swagger UI в Development
if (app.Environment.IsDevelopment())
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

// /users/me, /users/{id}/block, /users/{id}/unblock
app.MapUsersEndpoints();

// Seed dev-админа: в Development создаём/повышаем пользователя из конфига (Seed:Admin).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
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
