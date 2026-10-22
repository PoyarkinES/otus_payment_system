using Otus.PaymentSystem.Identity.Domain.Events;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;
using Otus.PaymentSystem.SharedKernel.Events;

namespace Otus.PaymentSystem.Identity.Domain.Aggregates;

/// <summary>
/// Корень агрегата User. Инкапсулирует состояние пользователя и его инварианты.
/// Изменение состояния — только через методы агрегата.
/// </summary>
public class User
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserStatus Status { get; private set; }
    public UserRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int Version { get; private set; }

    /// <summary>Доменные события, накопленные агрегатом.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // Для EF Core / сериализации.
    private User()
    {
    }

    private User(Email email, string passwordHash)
    {
        Id = Guid.NewGuid();
        Email = email;
        PasswordHash = passwordHash;
        Status = UserStatus.Active;
        Role = UserRole.User;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        Version = 0;
    }

    /// <summary>
    /// Регистрирует нового пользователя. Пароль принимается уже в виде хэша
    /// (хэширование — ответственность Infrastructure, Domain не зависит от криптографии).
    /// </summary>
    public static User Register(Email email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("PasswordHash не может быть пустым.");

        var user = new User(email, passwordHash);
        user.Raise(new UserRegistered(user.Id, user.Email.Value, user.CreatedAt));
        return user;
    }

    /// <summary>Блокирует пользователя. Повторная блокировка запрещена.</summary>
    public void Block(Guid blockedBy)
    {
        if (Status == UserStatus.Blocked)
            throw new DomainException("Пользователь уже заблокирован.");

        Status = UserStatus.Blocked;
        Touch();
        Raise(new UserBlocked(Id, blockedBy, UpdatedAt));
    }

    /// <summary>Разблокирует пользователя. Разблокировать можно только заблокированного.</summary>
    public void Unblock(Guid unblockedBy)
    {
        if (Status != UserStatus.Blocked)
            throw new DomainException("Пользователь не заблокирован.");

        Status = UserStatus.Active;
        Touch();
        Raise(new UserUnblocked(Id, unblockedBy, UpdatedAt));
    }

    /// <summary>
    /// Меняет email. Новый email не должен совпадать с текущим.
    /// Уникальность проверяется на уровне Application (IUserRepository).
    /// </summary>
    public void ChangeEmail(Email newEmail)
    {
        if (newEmail == Email)
            throw new DomainException("Новый email совпадает с текущим.");

        var old = Email;
        Email = newEmail;
        Touch();
        Raise(new EmailChanged(Id, old.Value, newEmail.Value, UpdatedAt));
    }

    /// <summary>Меняет хэш пароля (сам пароль уже захэширован).</summary>
    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new DomainException("PasswordHash не может быть пустым.");

        if (newPasswordHash == PasswordHash)
            throw new DomainException("Новый пароль совпадает с текущим.");

        PasswordHash = newPasswordHash;
        Touch();
        Raise(new PasswordChanged(Id, UpdatedAt));
    }

    /// <summary>Повышает пользователя до администратора. Идемпотентно.</summary>
    public void PromoteToAdmin()
    {
        if (Role == UserRole.Admin)
            return;

        Role = UserRole.Admin;
        Touch();
    }

    /// <summary>Очищает накопленные доменные события (вызывается после публикации/сохранения).</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }

    private void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
