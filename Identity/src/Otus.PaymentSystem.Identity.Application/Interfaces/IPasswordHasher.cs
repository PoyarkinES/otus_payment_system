namespace Otus.PaymentSystem.Identity.Application.Interfaces;

/// <summary>
/// Порт хеширования и проверки паролей. Реализация — в Infrastructure.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Возвращает хеш пароля для сохранения.</summary>
    string Hash(string password);

    /// <summary>Проверяет, что пароль соответствует сохранённому хешу.</summary>
    bool Verify(string password, string passwordHash);
}
