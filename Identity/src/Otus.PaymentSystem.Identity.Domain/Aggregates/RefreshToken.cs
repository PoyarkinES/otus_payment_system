using Otus.PaymentSystem.Identity.Domain.Errors;
using Otus.PaymentSystem.Identity.Domain.Exceptions;

namespace Otus.PaymentSystem.Identity.Domain.Aggregates
{
    /// <summary>
    /// Представляет токен обновления, используемый для аутентификации и управления сессиями.
    /// </summary>
    public class RefreshToken
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string TokenHash { get; private set; } = null!;
        public DateTimeOffset ExpiresAt { get; private set; }
        public DateTimeOffset? RevokedAt { get; private set; }
        public Guid? ReplacedByTokenId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }

        /// <summary>
        /// Возвращает значение, указывающее, был ли токен обновления ротационным.
        /// </summary>
        /// <remarks>
        /// Токен считается ротационным, если он был отозван и заменён другим токеном.
        /// </remarks>
        public bool WasRotated => RevokedAt.HasValue && ReplacedByTokenId.HasValue;

        private RefreshToken() { }

        private RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAt)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            TokenHash = tokenHash;
            ExpiresAt = expiresAt;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        public static RefreshToken Issues(Guid userId, string tokenHash, DateTimeOffset expiresAt)
        {
            if (userId == Guid.Empty)
            {
                throw new DomainException("UserId не может быть пустым.");
            }
            if (string.IsNullOrWhiteSpace(tokenHash))
            {
                throw new DomainException("Хеш токена не может быть пустым.");
            }

            return expiresAt <= DateTimeOffset.UtcNow
                ? throw new DomainException("Срок действия токена должен быть в будущем.")
                : new RefreshToken(userId, tokenHash, expiresAt);
        }

        /// <summary>
        /// Определяет, активен ли токен обновления.
        /// </summary>
        /// <param name="now">Текущая дата и время, используемые для проверки срока действия токена.</param>
        /// <returns>
        /// <see langword="true"/>, если токен не был отозван и срок его действия ещё не истёк; 
        /// в противном случае — <see langword="false"/>.
        /// </returns>
        public bool IsActive(DateTimeOffset now)
        {
            return !RevokedAt.HasValue && ExpiresAt > now;
        }

        /// <summary>
        /// Проверяет, активен ли токен обновления, и выбрасывает исключение, если он не активен.
        /// </summary>
        /// <param name="now">Текущая дата и время, используемые для проверки срока действия токена.</param>
        /// <exception cref="DomainException">
        /// Выбрасывается, если токен был отозван или срок его действия истёк.
        /// </exception>
        public void EnsureActive(DateTimeOffset now)
        {
            if (!IsActive(now))
            {
                throw new DomainException("Токен обновления не активен.", DomainError.Unauthorized);
            }
        }

        /// <summary>
        /// Отзывает токен обновления, делая его недействительным.
        /// </summary>
        /// <param name="revokedAt">
        /// Дата и время отзыва токена.
        /// </param>
        /// <param name="replacedByTokenId">
        /// Идентификатор нового токена, заменяющего текущий токен, если применимо.
        /// </param>
        /// <exception cref="DomainException">
        /// Выбрасывается, если токен уже был отозван.
        /// </exception>
        public void Revoke(Guid? replacedByTokenId = null)
        {
            if (RevokedAt.HasValue)
            {
                return;
            }
            RevokedAt = DateTimeOffset.UtcNow;
            ReplacedByTokenId = replacedByTokenId;
        }
    }
}
