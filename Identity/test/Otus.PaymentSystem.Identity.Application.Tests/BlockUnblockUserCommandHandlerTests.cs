using Otus.PaymentSystem.Identity.Application.Commands.BlockUser;
using Otus.PaymentSystem.Identity.Application.Commands.UnblockUser;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Application.Tests;

public class BlockUnblockUserCommandHandlerTests
{
    [Fact]
    public async Task Block_ActiveUser_SetsBlockedAndSaves()
    {
        var users = Substitute.For<IUserRepository>();
        var user = User.Register(new Email("user@example.com"), "hash");
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var handler = new BlockUserCommandHandler(users);

        await handler.Handle(new BlockUserCommand(user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(UserStatus.Blocked, user.Status);
        await users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Block_UnknownUser_ThrowsDomainException()
    {
        var users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        var handler = new BlockUserCommandHandler(users);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new BlockUserCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Unblock_BlockedUser_SetsActiveAndSaves()
    {
        var users = Substitute.For<IUserRepository>();
        var user = User.Register(new Email("user@example.com"), "hash");
        user.Block(Guid.NewGuid());
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var handler = new UnblockUserCommandHandler(users);

        await handler.Handle(new UnblockUserCommand(user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(UserStatus.Active, user.Status);
        await users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
