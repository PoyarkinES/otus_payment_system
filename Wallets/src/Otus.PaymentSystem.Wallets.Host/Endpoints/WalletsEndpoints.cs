using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MediatR;
using Otus.PaymentSystem.Wallets.Application.Commands.CreateWallet;
using Otus.PaymentSystem.Wallets.Application.Contracts;
using Otus.PaymentSystem.Wallets.Application.Queries.GetWallet;
using Otus.PaymentSystem.Wallets.Host.Contracts;
using Otus.PaymentSystem.Wallets.Application.Commands.DepositWallet;
using Otus.PaymentSystem.Wallets.Application.Commands.WithdrawWallet;
using Otus.PaymentSystem.Wallets.Application.Queries.GetWalletOperations;
using Otus.PaymentSystem.Wallets.Application.Queries.ListUserWallets;

namespace Otus.PaymentSystem.Wallets.Host.Endpoints;

public static class WalletsEndpoints
{
    public static IEndpointRouteBuilder MapWalletsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/wallets")
            .WithTags("Wallets")
            .RequireAuthorization();

        group.MapPost("", Create)
            .WithName("CreateWallet")
            .Produces<WalletResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/deposit", Deposit)
            .WithName("DepositWallet")
            .Produces<WalletResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/withdraw", Withdraw)
            .WithName("WithdrawWallet")
            .Produces<WalletResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", Get)
            .WithName("GetWallet")
            .Produces<WalletResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/operations", GetWalletOperations)
            .WithName("GetWalletOperations")
            .Produces<WalletOperationsPageResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", ListUserWallets)
            .WithName("ListUserWallets")
            .Produces<IReadOnlyCollection<WalletResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

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
        WalletResult result = await mediator.Send(new GetWalletQuery(id, userId.Value), cancellationToken);
        return Results.Ok(result);
    }

    internal static Guid? GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static async Task<IResult> Deposit(
        Guid id,
        WalletOperationRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetUserId(user);
        if (userId is null) return Results.Unauthorized();

        WalletResult result = await mediator.Send(
            new DepositWalletCommand(id, userId.Value, request.Amount, idempotencyKey),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> Withdraw(
        Guid id,
        WalletOperationRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetUserId(user);
        if (userId is null) return Results.Unauthorized();

        var result = await mediator.Send(
            new WithdrawWalletCommand(id, userId.Value, request.Amount, idempotencyKey),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetWalletOperations(
        Guid id,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetUserId(user);
        if (userId is null) return Results.Unauthorized();

        page ??= 1;
        pageSize ??= 20;

        WalletOperationsPageResult result = await mediator.Send(
            new GetWalletOperationsQuery(id, userId.Value, page.Value, pageSize.Value),
            cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListUserWallets(ClaimsPrincipal user, IMediator mediator, CancellationToken cancellationToken)
    {
        Guid? userId = GetUserId(user);
        if (userId is null) return Results.Unauthorized();
        
        IReadOnlyCollection<WalletResult> result = await mediator.Send(new ListUserWalletsQuery(userId.Value), cancellationToken);
        return Results.Ok(result);
    }

}
