using FluentValidation;
using MediatR;
using Otus.PaymentSystem.Identity.Application.Commands.Login;
using Otus.PaymentSystem.Identity.Application.Commands.Register;
using Otus.PaymentSystem.Identity.Host.Contracts.Auth;

namespace Otus.PaymentSystem.Identity.Host.Endpoints;

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

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        RegisterRequestValidator validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await mediator.Send(new RegisterCommand(request.Email, request.Password), ct);
        return Results.Created($"/users/{result.UserId}", result);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginRequestValidator validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await mediator.Send(new LoginCommand(request.Email, request.Password), ct);
        return Results.Ok(result);
    }
}
