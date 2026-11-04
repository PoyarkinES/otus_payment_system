using MediatR;
using Otus.PaymentSystem.Identity.Application.Commands.Login;
using Otus.PaymentSystem.Identity.Application.Commands.Register;
using Otus.PaymentSystem.Identity.Host.Contracts.Auth;
using Otus.PaymentSystem.Identity.Host.Security;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.AspNetCore.Filters;
using Otus.PaymentSystem.Identity.Application.Commands.Auth;
using Otus.PaymentSystem.Identity.Application.Commands.Logout;
using Otus.PaymentSystem.Identity.Application.Commands.Refresh;

namespace Otus.PaymentSystem.Identity.Host.Endpoints;

/// <summary>Ответ GET /auth/me.</summary>
public record MeResult(Guid UserId, string Email);

/// <summary>Эндпоинты аутентификации: /auth/register, /auth/login.</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .AllowAnonymous()
            .Produces<RegisterResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .WithName("Login")
            .AllowAnonymous()
            .Produces<AuthTokens>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/me", Me)
            .WithName("Me")
            .RequireAuthorization()
            .Produces<MeResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<LogoutRequest>>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .WithName("Refresh")
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<RefreshRequest>>()
            .Produces<AuthTokens>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    /// <summary>Данные текущего пользователя из claims (без похода в БД).</summary>
    private static IResult Me(System.Security.Claims.ClaimsPrincipal user)
    {
        Guid? userId = user.GetUserId();
        var email = user.GetEmail();
        if (userId is null || string.IsNullOrEmpty(email))
            return Results.Unauthorized();

        return Results.Ok(new MeResult(userId.Value, email));
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        ILogger logger = loggerFactory.CreateLogger("Auth");
        RegisterResult result = await mediator.Send(new RegisterCommand(request.Email, request.Password), ct);
        logger.LogInformation("User registered {UserId}", result.UserId);
        return Results.Created($"/users/{result.UserId}", result);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        ILogger logger = loggerFactory.CreateLogger("Auth");

        try
        {
            AuthTokens result = await mediator.Send(new LoginCommand(request.Email, request.Password), ct);
            logger.LogInformation("User logged in");
            return Results.Ok(result);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Login failed: {Reason}", ex.Message);
            throw;
        }
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        System.Security.Claims.ClaimsPrincipal user,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        Guid? userId = user.GetUserId();
        if (userId is null)
            return Results.Unauthorized();

        ILogger logger = loggerFactory.CreateLogger("Auth");
        await mediator.Send(new LogoutCommand(userId.Value, request.RefreshToken), ct);
        logger.LogInformation("Session revoked {UserId}", userId.Value);

        return Results.NoContent();
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        ILogger logger = loggerFactory.CreateLogger("Auth");

        try
        {
            AuthTokens result = await mediator.Send(new RefreshCommand(request.RefreshToken), ct);
            logger.LogInformation("Session refreshed");
            return Results.Ok(result);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Refresh failed: {Reason}", ex.Message);
            throw;
        }
    }
}
