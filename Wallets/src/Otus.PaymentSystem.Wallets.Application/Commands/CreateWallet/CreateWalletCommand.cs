using MediatR;
using Otus.PaymentSystem.Wallets.Application.Contracts;

namespace Otus.PaymentSystem.Wallets.Application.Commands.CreateWallet;

public sealed record CreateWalletCommand(Guid UserId, string Currency) : IRequest<WalletResult>;
