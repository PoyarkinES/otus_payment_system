using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Otus.PaymentSystem.Identity.Application.Commands.Login;
using Otus.PaymentSystem.Identity.Application.Commands.Register;
using Otus.PaymentSystem.Identity.Host.Contracts.Auth;
using Otus.PaymentSystem.Identity.Host.Security;
using Otus.PaymentSystem.Identity.Domain.Exceptions;

namespace Otus.PaymentSystem.Identity.Host.Endpoints;

/// <summary>Ответ GET /auth/me.</summary>
public record MeResult(Guid UserId, string Email);

/// <summary>Эндпоинты аутентификации: /auth/register, /auth/login.</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .Produces<RegisterResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces<LoginResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/me", Me)
            .WithName("Me")
            .RequireAuthorization()
            .Produces<MeResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    /// <summary>Данные текущего пользователя из claims (без похода в БД).</summary>
    private static IResult Me(System.Security.Claims.ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        var email = user.GetEmail();
        if (userId is null || string.IsNullOrEmpty(email))
            return Results.Unauthorized();

        return Results.Ok(new MeResult(userId.Value, email));
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        RegisterRequestValidator validator,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth");
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await mediator.Send(new RegisterCommand(request.Email, request.Password), ct);
        logger.LogInformation("User registered {UserId}", result.UserId);
        return Results.Created($"/users/{result.UserId}", result);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginRequestValidator validator,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth");
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        try
        {
            var result = await mediator.Send(new LoginCommand(request.Email, request.Password), ct);
            logger.LogInformation("User logged in");
            return Results.Ok(result);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Login failed: {Reason}", ex.Message);
            throw;
        }
    }
}
