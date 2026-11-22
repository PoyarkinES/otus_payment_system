using MediatR;
using Otus.PaymentSystem.Wallets.Application.Contracts;
using Otus.PaymentSystem.Wallets.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;

namespace Otus.PaymentSystem.Wallets.Application.Queries.ListUserWallets
{
    public sealed class ListUserWalletsQueryHandler(IWalletRepository wallets) : IRequestHandler<ListUserWalletsQuery, IReadOnlyCollection<WalletResult>>
    {
        public async Task<IReadOnlyCollection<WalletResult>> Handle(ListUserWalletsQuery request, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<Wallet> walletItems = await wallets.ListByUserAsync(request.UserId, cancellationToken);

            return [.. walletItems.Select(WalletResult.From)];
        }
    }
}
