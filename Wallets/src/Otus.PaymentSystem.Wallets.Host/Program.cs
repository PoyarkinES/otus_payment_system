using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Otus.PaymentSystem.AspNetCore;
using Otus.PaymentSystem.AspNetCore.Middleware;
using Otus.PaymentSystem.AspNetCore.Security;
using Otus.PaymentSystem.Wallets.Application.Commands.CreateWallet;
using Otus.PaymentSystem.Wallets.Host.Endpoints;
using Otus.PaymentSystem.Wallets.Host.Middleware;
using Otus.PaymentSystem.Wallets.Infrastructure;
using Otus.PaymentSystem.Wallets.Infrastructure.Persistence;
using Serilog;
using Serilog.Formatting.Compact;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

var swaggerEnabled = builder.Configuration.GetValue("Swagger:Enabled", builder.Environment.IsDevelopment());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Otus Payment System — Wallets API",
        Version = "v1",
        Description = "Кошельки, холды и балансовые операции. Публичный вход — через Gateway по путям /api/wallets/**."
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

    options.AddServer(new OpenApiServer
    {
        Url = "http://localhost:8080",
        Description = "Gateway (edge)"
    });
});

builder.Services.AddApiProblems(options =>
    options.IncludeExceptionDetails = builder.Environment.IsDevelopment());

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CreateWalletCommand>());

builder.Services.AddWalletsInfrastructure(builder.Configuration);

builder.Services.AddJwtBearerFromJwks(builder.Configuration);

builder.Services.AddAuthorization();

WebApplication app = builder.Build();

var keyStore = app.Services.GetRequiredService<JwksKeyStore>();
if (!await keyStore.RefreshAsync())
{
    app.Logger.LogWarning("JWKS недоступен при старте: {JwksUri}. JWT-валидация будет ленивой.",
        app.Configuration["Jwt:JwksUri"]);
}

if (app.Configuration.GetValue("Database:AutoMigrate", false))
{
    using IServiceScope migrationScope = app.Services.CreateScope();
    await migrationScope.ServiceProvider
        .GetRequiredService<WalletsDbContext>()
        .Database.MigrateAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<UnhandledExceptionMiddleware>();
app.UseMiddleware<EmptyResponseProblemMiddleware>();

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<DomainExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("HealthCheck")
   .AllowAnonymous();

app.MapWalletsEndpoints();

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
