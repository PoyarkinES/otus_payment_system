using MediatR;
using Otus.PaymentSystem.Wallets.Application.Contracts;

namespace Otus.PaymentSystem.Wallets.Application.Commands.WithdrawWallet;

public sealed record WithdrawWalletCommand(
    Guid WalletId,
    Guid UserId,
    decimal Amount,
    Guid IdempotencyKey) : IRequest<WalletResult>;
