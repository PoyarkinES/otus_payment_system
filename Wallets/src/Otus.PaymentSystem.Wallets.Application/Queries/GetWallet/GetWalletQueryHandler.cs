using MediatR;
using Otus.PaymentSystem.Wallets.Application.Contracts;
using Otus.PaymentSystem.Wallets.Application.Interfaces;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;

namespace Otus.PaymentSystem.Wallets.Application.Queries.GetWallet;

public sealed class GetWalletQueryHandler : IRequestHandler<GetWalletQuery, WalletResult>
{
    private readonly IWalletRepository _wallets;

    public GetWalletQueryHandler(IWalletRepository wallets) => _wallets = wallets;

    public async Task<WalletResult> Handle(GetWalletQuery request, CancellationToken cancellationToken)
    {
        Wallet? wallet = await _wallets.GetByIdAsync(request.WalletId, cancellationToken);

        if (wallet is null)
            throw new DomainException("Кошелёк не найден.", DomainError.NotFound);

        if (wallet.UserId != request.RequesterUserId)
            throw new DomainException("Доступ к чужому кошельку запрещён.", DomainError.Forbidden);

        return new WalletResult(
            wallet.Id,
            wallet.UserId,
            wallet.Currency.Value,
            wallet.Available,
            wallet.Held,
            wallet.Total,
            wallet.Status.ToString(),
            wallet.Version,
            wallet.CreatedAt,
            wallet.UpdatedAt);
    }
}
