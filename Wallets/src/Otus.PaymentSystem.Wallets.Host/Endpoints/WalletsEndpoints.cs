using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Otus.PaymentSystem.Wallets.Application.Commands.CreateWallet;
using Otus.PaymentSystem.Wallets.Application.Contracts;
using Otus.PaymentSystem.Wallets.Application.Queries.GetWallet;
using Otus.PaymentSystem.Wallets.Host.Contracts;

namespace Otus.PaymentSystem.Wallets.Host.Endpoints;

public static class WalletsEndpoints
{
    public static IEndpointRouteBuilder MapWalletsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/wallets")
            .WithTags("Wallets")
            .RequireAuthorization();

        group.MapPost("", Create)
            .WithName("CreateWallet")
            .Produces<WalletResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", Get)
            .WithName("GetWallet")
            .Produces<WalletResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> Create(
        CreateWalletRequest request,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetUserId(user);
        if (userId is null) return Results.Unauthorized();
        var result = await mediator.Send(new CreateWalletCommand(userId.Value, request.Currency), cancellationToken);
        return Results.Created($"/api/wallets/{result.Id}", result);
    }

    private static async Task<IResult> Get(
        Guid id,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetUserId(user);
        if (userId is null) return Results.Unauthorized();
        var result = await mediator.Send(new GetWalletQuery(id, userId.Value), cancellationToken);
        return Results.Ok(result);
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        string? sub = user.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
