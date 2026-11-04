using Otus.PaymentSystem.Identity.Application.Commands.UpdateProfile;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Application.Tests;

public class UpdateProfileCommandHandlerTests
{
    private static (UpdateProfileCommandHandler Handler, IUserRepository Users,
        IPasswordHasher Hasher, IRefreshTokenRepository RefreshTokens) CreateSut()
    {
        var users = Substitute.For<IUserRepository>();
        var hasher = Substitute.For<IPasswordHasher>();
        var refreshTokens = Substitute.For<IRefreshTokenRepository>();
        return (new UpdateProfileCommandHandler(users, hasher, refreshTokens), users, hasher, refreshTokens);
    }
    
    [Fact]
    public async Task Handle_NewEmail_ChangesEmailAndSaves()
    {
        var (handler, users, _, _) = CreateSut();
        var user = User.Register(new Email("old@example.com"), "hash");
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await handler.Handle(new UpdateProfileCommand(user.Id, "new@example.com", null), CancellationToken.None);

        Assert.Equal("new@example.com", user.Email.Value);
        await users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmailTakenByAnotherUser_ThrowsDomainException()
    {
        var (handler, users, _, _) = CreateSut();
        var user = User.Register(new Email("old@example.com"), "hash");
        var other = User.Register(new Email("taken@example.com"), "hash2");
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(other);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new UpdateProfileCommand(user.Id, "taken@example.com", null), CancellationToken.None));
        await users.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewPassword_HashesAndChanges()
    {
        var (handler, users, hasher, _) = CreateSut();
        var user = User.Register(new Email("user@example.com"), "old-hash");
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        hasher.Hash("NewP@ssw0rd").Returns("new-hash");

        await handler.Handle(new UpdateProfileCommand(user.Id, null, "NewP@ssw0rd"), CancellationToken.None);

        Assert.Equal("new-hash", user.PasswordHash);
        await users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NothingToChange_StillSavesWithoutSideEffects()
    {
        var (handler, users, _, _) = CreateSut();
        var user = User.Register(new Email("user@example.com"), "hash");
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await handler.Handle(new UpdateProfileCommand(user.Id, null, null), CancellationToken.None);

        Assert.Equal("user@example.com", user.Email.Value);
        await users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewPassword_RevokesAllActiveTokens()
    {
        var (handler, users, hasher, refreshTokens) = CreateSut();
        var user = User.Register(new Email("user@example.com"), "old-hash");
        var active = RefreshToken.Issues(user.Id, "hash-1", DateTimeOffset.UtcNow.AddDays(30));

        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        hasher.Hash("NewP@ssw0rd").Returns("new-hash");
        refreshTokens
            .GetActiveByUserIdAsync(user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<RefreshToken> { active });

        await handler.Handle(new UpdateProfileCommand(user.Id, null, "NewP@ssw0rd"), CancellationToken.None);

        Assert.NotNull(active.RevokedAt);
    }

    [Fact]
    public async Task Handle_NewEmailOnly_DoesNotTouchSessions()
    {
        var (handler, users, _, refreshTokens) = CreateSut();
        var user = User.Register(new Email("old@example.com"), "hash");
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await handler.Handle(new UpdateProfileCommand(user.Id, "new@example.com", null), CancellationToken.None);

        await refreshTokens.DidNotReceive()
            .GetActiveByUserIdAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
