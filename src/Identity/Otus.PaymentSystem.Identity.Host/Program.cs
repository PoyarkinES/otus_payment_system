using Otus.PaymentSystem.Identity.Application.Commands.Register;
using Otus.PaymentSystem.Identity.Host.Contracts.Auth;
using Otus.PaymentSystem.Identity.Host.Endpoints;
using Otus.PaymentSystem.Identity.Host.Middleware;
using Otus.PaymentSystem.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MediatR: регистрируем все handlers из Application-сборки
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<RegisterCommand>());

// Infrastructure: EF Core InMemory, PasswordHasher, JWT
builder.Services.AddIdentityInfrastructure(builder.Configuration);

// Валидаторы запросов (FluentValidation)
builder.Services.AddSingleton<RegisterRequestValidator>();
builder.Services.AddSingleton<LoginRequestValidator>();

var app = builder.Build();

// Swagger UI в Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// DomainException -> ProblemDetails (401/403/409/400)
app.UseMiddleware<DomainExceptionMiddleware>();

// Health-check
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("HealthCheck");

// /auth/register, /auth/login
app.MapAuthEndpoints();

app.Run();
