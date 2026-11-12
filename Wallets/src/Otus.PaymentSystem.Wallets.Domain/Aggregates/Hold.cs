using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Otus.PaymentSystem.Wallets.Domain.Aggregates
{
    public class Hold   
    {
        public Guid Id { get; private set; }
        public Guid WalletId { get; private set; }
        public Guid TransferId { get; private set; }
        public Guid UserId { get; private set; }
        public decimal Amount { get; private set; }
        public HoldStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="Hold"/> с значениями по умолчанию.
        /// </summary>
        /// <remarks>
        /// Этот конструктор является приватным и предназначен для использования только инфраструктурой или ORM-инструментами.
        /// </remarks>
        private Hold() { }

        internal Hold(Guid walletId, Guid transferId, Guid userId, decimal amount)
        {
            if (amount <= 0)
                throw new DomainException("Сумма холда должна быть больше нуля.");

            if (transferId == Guid.Empty)
                throw new DomainException("TransferId не может быть пустым.");

            Id = Guid.NewGuid();
            WalletId = walletId;
            TransferId = transferId;
            UserId = userId;
            Amount = amount;
            Status = 0; // Assuming 0 is the initial status
            CreatedAt = DateTimeOffset.UtcNow;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>Помечает холд исполненным (деньги ушли получателю).</summary>
        internal void Complete()
        {
            Status = HoldStatus.Completed;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>Освобождает холд (деньги возвращаются отправителю).</summary>
        internal void Release()
        {
            Status = HoldStatus.Released;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        private void EnsureActive()
        {
            if (Status != 0)
                throw new DomainException($"Холд уже в состоянии {Status}.", DomainError.Conflict);
        }
    }
}
