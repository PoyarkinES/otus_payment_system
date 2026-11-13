using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Otus.PaymentSystem.Wallets.Domain.ValueObjects
{
    /// <summary>
    /// Денежная сумма с валютой. Масштаб — 4 знака, как numeric(18,4) в БД.
    /// Знак операции задаётся типом операции, а не отрицательной суммой.
    /// </summary>
    public sealed record Money
    {
        public decimal Amount { get; }

        public Currency Currency { get; }

        private Money(decimal amount, Currency currency)
        {
            Amount = amount;
            Currency = currency;
        }

        public static Money Create(decimal amount, Currency currency)
        {
            if (amount < 0)
                throw new ArgumentException("Сумма не может быть отрицательной.", nameof(amount));
            if (currency is null)
                throw new DomainException("Валюта обязательна.");
            if (decimal.Round(amount, 4) != amount)
                throw new DomainException("Сумма не может иметь больше 4 знаков после запятой.");

            return new Money(amount, currency);
        }

        public static Money Zero(Currency currency) => Create(0, currency);

        public override string ToString() => $"{Amount} {Currency}";
    }
}
