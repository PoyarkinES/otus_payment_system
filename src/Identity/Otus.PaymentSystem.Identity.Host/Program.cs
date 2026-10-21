using Otus.PaymentSystem.Identity.Application.Commands.Register;
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

var app = builder.Build();

// Swagger UI в Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health-check
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("HealthCheck");

app.Run();
