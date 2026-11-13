using MediatR;
using Otus.PaymentSystem.Wallets.Application.Contracts;
using Otus.PaymentSystem.Wallets.Application.Interfaces;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;

namespace Otus.PaymentSystem.Wallets.Application.Commands.CreateWallet;

public sealed class CreateWalletCommandHandler : IRequestHandler<CreateWalletCommand, WalletResult>
{
    private readonly IWalletRepository _wallets;

    public CreateWalletCommandHandler(IWalletRepository wallets) => _wallets = wallets;

    public async Task<WalletResult> Handle(CreateWalletCommand request, CancellationToken cancellationToken)
    {
        var currency = Currency.Create(request.Currency);

        if (await _wallets.ExistsAsync(request.UserId, currency, cancellationToken))
            throw new DomainException("Кошелёк с такой валютой уже существует.", DomainError.Conflict);

        var wallet = Wallet.Create(request.UserId, currency);
        await _wallets.AddAsync(wallet, cancellationToken);
        await _wallets.SaveChangesAsync(cancellationToken);

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
