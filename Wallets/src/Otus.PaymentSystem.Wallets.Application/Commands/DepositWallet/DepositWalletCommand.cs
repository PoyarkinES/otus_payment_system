using MediatR;
using Otus.PaymentSystem.Wallets.Application.Contracts;

namespace Otus.PaymentSystem.Wallets.Application.Commands.DepositWallet;

public sealed record DepositWalletCommand(
    Guid WalletId,
    Guid UserId,
    decimal Amount,
    Guid IdempotencyKey) : IRequest<WalletResult>;
