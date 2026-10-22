using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Application.Queries.GetProfile;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Application.Tests;

public class GetProfileQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingUser_ReturnsProfile()
    {
        var users = Substitute.For<IUserRepository>();
        var user = User.Register(new Email("user@example.com"), "hash");
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var handler = new GetProfileQueryHandler(users);

        var result = await handler.Handle(new GetProfileQuery(user.Id), CancellationToken.None);

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("user@example.com", result.Email);
        Assert.Equal(UserStatus.Active, result.Status);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsDomainException()
    {
        var users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        var handler = new GetProfileQueryHandler(users);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new GetProfileQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
