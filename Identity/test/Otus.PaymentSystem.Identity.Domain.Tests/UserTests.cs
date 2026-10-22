using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Events;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Domain.Tests;

public class UserTests
{
    private static Email NewEmail(string value = "user@example.com") => new(value);

    // ---------- Register ----------

    [Fact]
    public void Register_WithValidData_CreatesActiveUserWithHashedPassword()
    {
        var email = NewEmail();
        const string hash = "hash-1";

        var user = User.Register(email, hash);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(email, user.Email);
        Assert.Equal(hash, user.PasswordHash);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(0, user.Version);
        Assert.Equal(user.CreatedAt, user.UpdatedAt);
    }

    [Fact]
    public void Register_RaisesUserRegisteredEvent()
    {
        var user = User.Register(NewEmail(), "hash-1");

        var evt = Assert.Single(user.DomainEvents);
        var registered = Assert.IsType<UserRegistered>(evt);
        Assert.Equal(user.Id, registered.UserId);
        Assert.Equal("user@example.com", registered.Email);
        Assert.Equal(user.CreatedAt, registered.Timestamp);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Register_WithEmptyPasswordHash_ThrowsDomainException(string? hash)
    {
        Assert.Throws<DomainException>(() => User.Register(NewEmail(), hash!));
    }

    // ---------- Block ----------

    [Fact]
    public void Block_ActiveUser_SetsStatusAndRaisesEvent()
    {
        var user = User.Register(NewEmail(), "hash-1");
        user.ClearDomainEvents();
        var adminId = Guid.NewGuid();

        user.Block(adminId);

        Assert.Equal(UserStatus.Blocked, user.Status);
        var evt = Assert.IsType<UserBlocked>(Assert.Single(user.DomainEvents));
        Assert.Equal(user.Id, evt.UserId);
        Assert.Equal(adminId, evt.BlockedBy);
    }

    [Fact]
    public void Block_AlreadyBlockedUser_ThrowsDomainException()
    {
        var user = User.Register(NewEmail(), "hash-1");
        user.Block(Guid.NewGuid());

        Assert.Throws<DomainException>(() => user.Block(Guid.NewGuid()));
    }

    // ---------- Unblock ----------

    [Fact]
    public void Unblock_BlockedUser_SetsActiveAndRaisesEvent()
    {
        var user = User.Register(NewEmail(), "hash-1");
        user.Block(Guid.NewGuid());
        user.ClearDomainEvents();
        var adminId = Guid.NewGuid();

        user.Unblock(adminId);

        Assert.Equal(UserStatus.Active, user.Status);
        var evt = Assert.IsType<UserUnblocked>(Assert.Single(user.DomainEvents));
        Assert.Equal(user.Id, evt.UserId);
        Assert.Equal(adminId, evt.UnblockedBy);
    }

    [Fact]
    public void Unblock_ActiveUser_ThrowsDomainException()
    {
        var user = User.Register(NewEmail(), "hash-1");

        Assert.Throws<DomainException>(() => user.Unblock(Guid.NewGuid()));
    }

    // ---------- ChangePassword ----------

    [Fact]
    public void ChangePassword_WithNewHash_UpdatesAndRaisesEvent()
    {
        var user = User.Register(NewEmail(), "hash-1");
        user.ClearDomainEvents();

        user.ChangePassword("hash-2");

        Assert.Equal("hash-2", user.PasswordHash);
        var evt = Assert.IsType<PasswordChanged>(Assert.Single(user.DomainEvents));
        Assert.Equal(user.Id, evt.UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ChangePassword_WithEmptyHash_ThrowsDomainException(string? hash)
    {
        var user = User.Register(NewEmail(), "hash-1");

        Assert.Throws<DomainException>(() => user.ChangePassword(hash!));
    }

    [Fact]
    public void ChangePassword_WithSameHash_ThrowsDomainException()
    {
        var user = User.Register(NewEmail(), "hash-1");

        Assert.Throws<DomainException>(() => user.ChangePassword("hash-1"));
    }

    // ---------- Events plumbing ----------

    [Fact]
    public void ClearDomainEvents_RemovesAccumulatedEvents()
    {
        var user = User.Register(NewEmail(), "hash-1");

        user.ClearDomainEvents();

        Assert.Empty(user.DomainEvents);
    }
}
