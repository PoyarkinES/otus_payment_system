using MediatR;
using Otus.PaymentSystem.Wallets.Application.Commands.ConfirmHold;
using Otus.PaymentSystem.Wallets.Application.Commands.PlaceHold;
using Otus.PaymentSystem.Wallets.Application.Commands.ReleaseHold;
using Otus.PaymentSystem.Wallets.Application.Contracts;
using Otus.PaymentSystem.Wallets.Host.Contracts;
using System.Security.Claims;

namespace Otus.PaymentSystem.Wallets.Host.Endpoints
{
    public static class WalletHoldsEndpoints
    {
        public static IEndpointRouteBuilder MapWalletHoldsEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/wallets")
                .WithTags("Wallets")
                .RequireAuthorization();

            group.MapPost("/{walletId:guid}/holds", PlaceHold)
                .WithName("PlaceHold")
                .Produces<HoldResult>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);

            group.MapPost("/holds/{holdId:guid}/confirm", ConfirmHold)
                .WithName("ConfirmHold")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);

            group.MapPost("/holds/{holdId:guid}/release", ReleaseHold)
                .WithName("ReleaseHold")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);

            return app;
        }

        private static async Task<IResult> PlaceHold(
            Guid walletId, PlaceHoldRequest request, ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
        {
            Guid? userId = WalletsEndpoints.GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            HoldResult result = await mediator.Send(new PlaceHoldCommand(walletId, userId.Value, request.TransferId, request.Amount, request.RecipientUserId), ct);
            return Results.Ok(result);
        }

        private static async Task<IResult> ConfirmHold(
            Guid holdId, ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
        {
            Guid? userId = WalletsEndpoints.GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            await mediator.Send(new ConfirmHoldCommand(holdId, userId.Value), ct);
            return Results.NoContent();
        }

        private static async Task<IResult> ReleaseHold(
            Guid holdId, ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
        {
            Guid? userId = WalletsEndpoints.GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            await mediator.Send(new ReleaseHoldCommand(holdId, userId.Value), ct);
            return Results.NoContent();
        }
    }
}
