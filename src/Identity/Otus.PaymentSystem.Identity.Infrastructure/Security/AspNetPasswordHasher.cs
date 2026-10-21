using Microsoft.AspNetCore.Identity;
using Otus.PaymentSystem.Identity.Application.Interfaces;

namespace Otus.PaymentSystem.Identity.Infrastructure.Security;

/// <summary>
/// Реализация <see cref="IPasswordHasher"/> на базе ASP.NET Core Identity.
/// Использует PBKDF2 с солью — безопасный дефолт .NET.
/// Пароли не привязаны к сущности User, поэтому generic-параметр — object
/// (PasswordHasher не хранит состояние TUser, он нужен только для типизации).
/// </summary>
public class AspNetPasswordHasher(PasswordHasher<object> inner) : IPasswordHasher
{
    public string Hash(string password)
        => inner.HashPassword(new object(), password);

    public bool Verify(string password, string passwordHash)
    {
        var result = inner.VerifyHashedPassword(new object(), passwordHash, password);
        return result == PasswordVerificationResult.Success
            || result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
