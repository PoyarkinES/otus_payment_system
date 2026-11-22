using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using Otus.PaymentSystem.Wallets.Application.Contracts;

namespace Otus.PaymentSystem.Wallets.Application.Queries.ListUserWallets
{
    public sealed record ListUserWalletsQuery(Guid UserId) : IRequest<IReadOnlyCollection<WalletResult>>;
}
