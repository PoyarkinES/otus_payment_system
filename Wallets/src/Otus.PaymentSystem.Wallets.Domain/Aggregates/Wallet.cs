using Otus.PaymentSystem.SharedKernel.Events;
using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Events;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Otus.PaymentSystem.Wallets.Domain.Aggregates
{
    public class Wallet
    {
        private readonly List<Hold> _holds = [];
        private readonly List<WalletTransaction> _transactions = [];
        private readonly List<IDomainEvent> _domainEvents = [];

        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public Currency Currency { get; private set; } = null!;
        public decimal Available { get; private set; }
        public decimal Held { get; private set; }
        public WalletStatus Status { get; private set; }
        public int Version { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }
        public decimal Total => Available + Held;

        public IReadOnlyCollection<Hold> Holds => _holds.AsReadOnly();
        public IReadOnlyCollection<WalletTransaction> Transactions => _transactions.AsReadOnly();
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        /// <summary>
        /// Initializes a new instance of the Wallet class.
        /// </summary>
        private Wallet() { }

        private Wallet(Guid userId, Currency currency)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            Currency = currency;
            Available = 0m;
            Held = 0m;
            Status = WalletStatus.Active;
            Version = 0;
            CreatedAt = DateTimeOffset.UtcNow;
            UpdatedAt = CreatedAt;
        }

        public static Wallet Create(Guid userId, Currency currency)
        {
            if (userId == Guid.Empty)
                throw new DomainException("UserId не может быть пустым.");
            if (currency is null)
                throw new DomainException("Валюта обязательна.");

            var wallet = new Wallet(userId, currency);
            wallet.Raise(new WalletCreated(wallet.Id, userId, currency.Value, wallet.CreatedAt));
            return wallet;
        }

        /// <summary>Пополнение: деньги приходят извне, доступный баланс растёт.</summary>
        public void Deposit(Money amount, Guid operationId)
        {
            EnsureActive();
            EnsureSameCurrency(amount);
            EnsurePositive(amount, "пополнения");
            EnsureOperationId(operationId);

            var before = Total;
            Available += amount.Amount;
            AddTransaction(WalletTransactionType.TopUp, amount, before, Total, operationId, "Пополнение кошелька");
            Touch();
            Raise(new BalanceDeposited(Id, amount.Amount, Currency.Value, operationId, UpdatedAt));
        }

        /// <summary>Списание: деньги уходят из доступного баланса.</summary>
        public void Withdraw(Money amount, Guid operationId)
        {
            EnsureActive();
            EnsureSameCurrency(amount);
            EnsurePositive(amount, "списания");
            EnsureOperationId(operationId);

            if (Available < amount.Amount)
                throw new DomainException(
                    $"Недостаточно средств: доступно {Available:0.####}, требуется {amount.Amount:0.####}.",
                    DomainError.Conflict);

            var before = Total;
            Available -= amount.Amount;
            AddTransaction(WalletTransactionType.Withdrawal, amount, before, Total, operationId, "Списание с кошелька");
            Touch();
            Raise(new BalanceWithdrawn(Id, amount.Amount, Currency.Value, operationId, UpdatedAt));
        }

        /// <summary>
        /// Замораживает сумму под перевод: Available уменьшается, Held растёт.
        /// Повторный вызов с тем же transferId возвращает существующий холд.
        /// </summary>
        public Hold PlaceHold(Guid transferId, Money amount, Guid userId)
        {
            EnsureActive();
            EnsureSameCurrency(amount);
            EnsurePositive(amount, "холда");

            if (transferId == Guid.Empty)
                throw new DomainException("TransferId не может быть пустым.");
            if (userId == Guid.Empty)
                throw new DomainException("UserId не может быть пустым.");

            // Идемпотентность на уровне агрегата работает только если холды загружены.
            // В БД настоящий арбитр — UNIQUE(Holds.TransferId): гонку двух запросов ловит он.
            var existing = _holds.FirstOrDefault(h => h.TransferId == transferId);
            if (existing is not null)
                return existing;

            if (Available < amount.Amount)
                throw new DomainException(
                    $"Недостаточно средств для холда: доступно {Available:0.####}, требуется {amount.Amount:0.####}.",
                    DomainError.Conflict);

            var hold = new Hold(Id, transferId, userId, amount.Amount);
            _holds.Add(hold);
            Available -= amount.Amount;
            Held += amount.Amount;
            Touch();
            return hold;
        }
        
        /// <summary>Снимает заморозку: Held возвращается в Available. Идемпотентно.</summary>
        public void ReleaseHold(Guid holdId)
        {
            var hold = FindHold(holdId);

            if (hold.Status == HoldStatus.Released)
                return;

            if (hold.Status != HoldStatus.Active)
                throw new DomainException($"Холд в состоянии {hold.Status} нельзя снять.", DomainError.Conflict);

            Held -= hold.Amount;
            Available += hold.Amount;
            hold.Release();
            Touch();
        }

        /// <summary>
        /// Исполняет холд: замороженные деньги уходят из кошелька.
        /// Зачисление получателю — ReceiveTransfer у его кошелька, в той же транзакции.
        /// </summary>
        public void ConfirmHold(Guid holdId)
        {
            Hold hold = FindHold(holdId);

            if (hold.Status == HoldStatus.Completed)
                return;

            if (hold.Status != HoldStatus.Active)
                throw new DomainException($"Холд в состоянии {hold.Status} нельзя исполнить.", DomainError.Conflict);

            var before = Total;
            Held -= hold.Amount;
            hold.Complete();
            AddTransaction(
                WalletTransactionType.TransferOut,
                Money.Create(hold.Amount, Currency),
                before,
                Total,
                hold.TransferId,
                "Перевод получателю");
            Touch();
            Raise(new BalanceTransferredOut(Id, hold.Amount, Currency.Value, hold.TransferId, UpdatedAt));
        }

        public void ReceiveTransfer(Guid transferId, Money amount)
        {
            EnsureActive();
            EnsureSameCurrency(amount);
            EnsurePositive(amount, "зачисления");

            if (transferId == Guid.Empty)
                throw new DomainException("TransferId не может быть пустым.");

            var before = Total;
            Available += amount.Amount;
            AddTransaction(WalletTransactionType.TransferIn, amount, before, Total, transferId, "Зачисление перевода");
            Touch();
            Raise(new BalanceTransferredIn(Id, amount.Amount, Currency.Value, transferId, UpdatedAt));
        }

        public void ClearDomainEvents() => _domainEvents.Clear();

        private Hold FindHold(Guid holdId)
        {
            Hold? hold = _holds.Find(h => h.Id == holdId);
            return hold ?? throw new DomainException("Холд не найден.", DomainError.NotFound);
        }

        private void AddTransaction(WalletTransactionType type,
            Money amount,
            decimal balanceBefore,
            decimal balanceAfter,
            Guid? referenceId,
            string? description)
        {
            _transactions.Add(new WalletTransaction(
                Id, type, amount.Amount, Currency, balanceBefore, balanceAfter, referenceId, description, DateTimeOffset.UtcNow));

        }

        private void Touch()
        {
            UpdatedAt = DateTimeOffset.UtcNow;
            Version++;
        }

        private void EnsureActive()
        {
            if (Status != WalletStatus.Active)
                throw new DomainException("Кошелёк не активен.", DomainError.Conflict);
        }

        private void EnsureSameCurrency(Money amount)
        {
            if (amount.Currency != Currency)
                throw new DomainException(
                    $"Валюта операции {amount.Currency} не совпадает с валютой кошелька {Currency}.");
        }

        private static void EnsurePositive(Money amount, string operation)
        {
            if (amount.Amount <= 0)
                throw new DomainException($"Сумма {operation} должна быть больше нуля.");
        }

        private static void EnsureOperationId(Guid operationId)
        {
            if (operationId == Guid.Empty)
                throw new DomainException("OperationId не может быть пустым.");
        }

        private void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    }
}
