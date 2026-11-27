using MediatR;
using Otus.PaymentSystem.Wallets.Application.Contracts;
using Otus.PaymentSystem.Wallets.Application.Interfaces;
using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;

namespace Otus.PaymentSystem.Wallets.Application.Queries.GetWalletOperations
{
    public sealed class GetWalletOperationsQueryHandler(IWalletRepository wallets)
        : IRequestHandler<GetWalletOperationsQuery, WalletOperationsPageResult>
    {
        public async Task<WalletOperationsPageResult> Handle(GetWalletOperationsQuery query, CancellationToken cancellationToken)
        {
            if (query.Page < 1)
            {
                throw new DomainException("Номер страницы должен быть не менее 1.", DomainError.Validation);
            }

            if (query.PageSize is < 1 or > 100)
            {
                throw new DomainException("Размер страницы должен быть от 1 до 100.", DomainError.Validation);
            }

            if (query.Type.HasValue && !Enum.IsDefined(typeof(WalletTransactionType), query.Type.Value))
            {
                throw new DomainException("Неизвестный тип операции.", DomainError.Validation);
            }

            if (query.From is { } from && query.To is { } to && from > to)
            {
                throw new DomainException("Начало периода не может быть позже его окончания.", DomainError.Validation);
            }

            Wallet? wallet = await wallets.GetByIdLightAsync(query.WalletId, cancellationToken);
            if (wallet == null)
            {
                throw new DomainException("Кошелёк не найден.", DomainError.NotFound);
            }

            if (wallet.UserId != query.RequestUserId)
            {
                throw new DomainException("Доступ к чужой истории запрещён.", DomainError.Forbidden);
            }

            PagedWalletTransactions transactionsPage = await wallets.GetTransactionsPageAsync(
                query.WalletId,
                query.Page,
                query.PageSize,
                query.Type,
                query.From,
                query.To,
                cancellationToken);
            var items = transactionsPage.Items.Select(WalletOperationResult.From).ToList();

            return new WalletOperationsPageResult(
                items,
                query.Page,
                query.PageSize,
                transactionsPage.TotalCount);
        }
    }
}
