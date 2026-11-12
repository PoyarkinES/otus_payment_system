using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Otus.PaymentSystem.Wallets.Domain.Aggregates
{
    public class WalletTransaction
    {
        public Guid Id { get; private set; }
        public Guid WalletId { get; private set; }
        public WalletTransactionType Type { get; private set; }
        public decimal Amount { get; private set; }
        public Currency Currency { get; private set; } = null!;
        public decimal BalanceBefore { get; private set; }
        public decimal BalanceAfter { get; private set; }
        public Guid? ReferenceId { get; private set; }
        public string? Description { get; private set; } = null!;
        public DateTimeOffset CreatedAt { get; private set; }

        /// <summary>
        /// Initializes a new instance of the WalletTransaction class.
        /// </summary>
        private WalletTransaction() { }

        internal WalletTransaction(
            Guid walletId,
            WalletTransactionType type,
            decimal amount,
            Currency currency,
            decimal balanceBefore,
            decimal balanceAfter,
            Guid? referenceId,
            string? description,
            DateTimeOffset createdAt)
        {
            if (amount <= 0)
                throw new DomainException("Сумма операции должна быть больше нуля.");

            var expected = balanceBefore + SignedAmount(type, amount);
            if (balanceAfter != expected)
                throw new DomainException(
                    $"Нарушен баланс операции {type}: ожидалось {expected:0.####}, получено {balanceAfter:0.####}.");

            Id = Guid.NewGuid();
            WalletId = walletId;
            Type = type;
            Amount = amount;
            Currency = currency;
            BalanceBefore = balanceBefore;
            BalanceAfter = balanceAfter;
            ReferenceId = referenceId;
            Description = description;
            CreatedAt = createdAt;
        }

        private static decimal SignedAmount(WalletTransactionType type, decimal amount)
        {
            return type switch
            {
                WalletTransactionType.TopUp
                    or WalletTransactionType.TransferIn
                    or WalletTransactionType.Refund => +amount,
                WalletTransactionType.Withdrawal
                    or WalletTransactionType.TransferOut
                    or WalletTransactionType.Fee => -amount,
                _ => throw new ArgumentException($"Invalid transaction type: {type}", nameof(type)),
            };
        }
    }
}
