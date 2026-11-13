using MediatR;
using Otus.PaymentSystem.Wallets.Application.Contracts;

namespace Otus.PaymentSystem.Wallets.Application.Queries.GetWallet;

public sealed record GetWalletQuery(Guid WalletId, Guid RequesterUserId) : IRequest<WalletResult>;
