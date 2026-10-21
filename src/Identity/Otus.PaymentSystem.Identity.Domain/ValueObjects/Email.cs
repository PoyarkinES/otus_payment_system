using Otus.PaymentSystem.Identity.Domain.Exceptions;

namespace Otus.PaymentSystem.Identity.Domain.ValueObjects;

/// <summary>
/// Email как value object: валидируется при создании и нормализуется (trim + lowercase).
/// Сравнение — по значению.
/// </summary>
public sealed record Email
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email не может быть пустым.");

        var normalized = value.Trim().ToLowerInvariant();

        if (!IsValid(normalized))
            throw new DomainException($"Некорректный email: '{value}'.");

        Value = normalized;
    }

    private static bool IsValid(string value)
    {
        // Простая проверка: ровно один '@', непустые локальная часть и домен,
        // в домене есть точка, нет пробелов. Полноценный RFC-парсер здесь не нужен —
        // это доменная валидация формата, а не контроль доставки.
        var atIndex = value.IndexOf('@');
        if (atIndex <= 0 || atIndex != value.LastIndexOf('@'))
            return false;

        var local = value[..atIndex];
        var domain = value[(atIndex + 1)..];

        if (local.Length == 0 || domain.Length == 0)
            return false;

        return !value.Contains(' ') && domain.Contains('.');
    }

    public override string ToString() => Value;
}
