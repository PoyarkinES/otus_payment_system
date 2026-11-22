using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using Otus.PaymentSystem.Wallets.Application.Contracts;

namespace Otus.PaymentSystem.Wallets.Application.Queries.GetWalletOperations
{
    public sealed record GetWalletOperationsQuery(Guid WalletId, Guid RequestUserId, int Page, int PageSize)
        : IRequest<WalletOperationsPageResult>;
}
