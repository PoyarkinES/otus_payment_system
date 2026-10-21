using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Otus.PaymentSystem.Identity.Host.Security;

/// <summary>Хелперы для извлечения данных пользователя из ClaimsPrincipal.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Возвращает UserId из claim "sub" или null, если claim отсутствует/невалиден.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>Возвращает email из claim "email" или null.</summary>
    public static string? GetEmail(this ClaimsPrincipal principal)
        => principal.FindFirstValue(JwtRegisteredClaimNames.Email)
           ?? principal.FindFirstValue(ClaimTypes.Email);
}
