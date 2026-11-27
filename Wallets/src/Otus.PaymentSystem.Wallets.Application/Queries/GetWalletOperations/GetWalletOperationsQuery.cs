using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using Otus.PaymentSystem.Wallets.Application.Contracts;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;

namespace Otus.PaymentSystem.Wallets.Application.Queries.GetWalletOperations
{
    public sealed record GetWalletOperationsQuery(
        Guid WalletId,
        Guid RequestUserId,
        int Page,
        int PageSize,
        WalletTransactionType? Type = null,
        DateOnly? From = null,
        DateOnly? To = null)
        : IRequest<WalletOperationsPageResult>;
}
