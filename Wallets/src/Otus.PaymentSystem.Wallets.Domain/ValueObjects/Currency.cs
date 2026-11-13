using System;
using System.Collections.Generic;
using System.Text;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;

namespace Otus.PaymentSystem.Wallets.Domain.ValueObjects
{
    public sealed record Currency
    {
        private static readonly HashSet<string> Supported = new(StringComparer.Ordinal) { "RUB" };

        public string Value { get; }
        public static Currency Rub => Create("RUB");

        private Currency(string value) => Value = value;

        public static Currency Create(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new DomainException("Валюта не может быть пустой.");

            var normalized = value.Trim().ToUpperInvariant();

            if (normalized.Length != 3 || !normalized.All(char.IsAsciiLetterUpper))
                throw new DomainException($"Некорректный код валюты: '{value}'.");

            return !Supported.Contains(normalized) ? throw new DomainException($"Валюта '{normalized}' не поддерживается в MVP.") : new Currency(normalized);
        }

        public override string ToString() => Value;
    }
}
