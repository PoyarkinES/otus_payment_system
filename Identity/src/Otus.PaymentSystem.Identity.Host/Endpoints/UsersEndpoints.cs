using MediatR;
using Otus.PaymentSystem.AspNetCore.Filters;
using Otus.PaymentSystem.Identity.Application.Commands.BlockUser;
using Otus.PaymentSystem.Identity.Application.Commands.UnblockUser;
using Otus.PaymentSystem.Identity.Application.Commands.UpdateProfile;
using Otus.PaymentSystem.Identity.Application.Queries.GetProfile;
using Otus.PaymentSystem.Identity.Host.Contracts.Users;
using Otus.PaymentSystem.Identity.Host.Security;

namespace Otus.PaymentSystem.Identity.Host.Endpoints;

/// <summary>Эндпоинты управления пользователями.</summary>
public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/users").WithTags("Users").RequireAuthorization();

        group.MapGet("/me", GetMe)
            .WithName("GetMyProfile")
            .Produces<GetProfileResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/me", UpdateMe)
            .WithName("UpdateMyProfile")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<UpdateProfileRequest>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Админ видит чужой профиль. Сервисной аутентификации в MVP нет, поэтому
        // единственный доступ к чужому Id — по роли из access-токена.
        group.MapGet("/{id:guid}", GetById)
            .WithName("GetUserById")
            .RequireAuthorization("AdminOnly")
            .Produces<GetProfileResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/block", Block)
            .WithName("BlockUser")
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/unblock", Unblock)
            .WithName("UnblockUser")
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> GetMe(System.Security.Claims.ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        Guid? userId = user.GetUserId();
        if (userId is null) return Results.Unauthorized();

        GetProfileResult result = await mediator.Send(new GetProfileQuery(userId.Value), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateMe(
        UpdateProfileRequest request,
        System.Security.Claims.ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken ct)
    {
        Guid? userId = user.GetUserId();
        if (userId is null) return Results.Unauthorized();

        await mediator.Send(new UpdateProfileCommand(userId.Value, request.Email, request.Password), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetById(Guid id, IMediator mediator, CancellationToken ct)
    {
        GetProfileResult result = await mediator.Send(new GetProfileQuery(id), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Block(
        Guid id,
        System.Security.Claims.ClaimsPrincipal user,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        ILogger logger = loggerFactory.CreateLogger("Users");

        Guid? adminId = user.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        await mediator.Send(new BlockUserCommand(id, adminId.Value), ct);
        logger.LogInformation("User blocked {TargetUserId} by {AdminId}", id, adminId.Value);
        return Results.NoContent();
    }

    private static async Task<IResult> Unblock(
        Guid id,
        System.Security.Claims.ClaimsPrincipal user,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        ILogger logger = loggerFactory.CreateLogger("Users");

        Guid? adminId = user.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        await mediator.Send(new UnblockUserCommand(id, adminId.Value), ct);
        logger.LogInformation("User unblocked {TargetUserId} by {AdminId}", id, adminId.Value);
        return Results.NoContent();
    }
}
