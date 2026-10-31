using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
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
        var group = app.MapGroup("/users").WithTags("Users").RequireAuthorization();

        group.MapGet("/me", GetMe)
            .WithName("GetMyProfile")
            .Produces<GetProfileResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/me", UpdateMe)
            .WithName("UpdateMyProfile")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/block", Block)
            .WithName("BlockUser")
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/unblock", Unblock)
            .WithName("UnblockUser")
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetMe(System.Security.Claims.ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var result = await mediator.Send(new GetProfileQuery(userId.Value), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateMe(
        UpdateProfileRequest request,
        UpdateProfileRequestValidator validator,
        System.Security.Claims.ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        await mediator.Send(new UpdateProfileCommand(userId.Value, request.Email, request.Password), ct);
        return Results.NoContent();
    }
    private static async Task<IResult> Block(
        Guid id,
        System.Security.Claims.ClaimsPrincipal user,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Users");

        var adminId = user.GetUserId();
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
        var logger = loggerFactory.CreateLogger("Users");

        var adminId = user.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        await mediator.Send(new UnblockUserCommand(id, adminId.Value), ct);
        logger.LogInformation("User unblocked {TargetUserId} by {AdminId}", id, adminId.Value);
        return Results.NoContent();
    }
}
