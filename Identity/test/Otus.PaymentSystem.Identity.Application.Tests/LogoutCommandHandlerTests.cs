using Otus.PaymentSystem.Identity.Application.Commands.Logout;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;

namespace Otus.PaymentSystem.Identity.Application.Tests;

public class LogoutCommandHandlerTests
{
    private const string RawToken = "raw-token";
    private const string Hash = "hash-token";

    private static (LogoutCommandHandler Handler, IRefreshTokenRepository Repository) CreateSut()
    {
        var factory = Substitute.For<IRefreshTokenFactory>();
        var repository = Substitute.For<IRefreshTokenRepository>();
        factory.Hash(RawToken).Returns(Hash);
        return (new LogoutCommandHandler(factory, repository), repository);
    }

    private static RefreshToken NewToken(Guid userId) =>
        RefreshToken.Issues(userId, Hash, DateTimeOffset.UtcNow.AddDays(30));

    [Fact]
    public async Task Logout_WithActiveToken_RevokesIt()
    {
        var (handler, repository) = CreateSut();
        var userId = Guid.NewGuid();
        var token = NewToken(userId);
        repository.GetByHashAsync(Hash, Arg.Any<CancellationToken>()).Returns(token);

        await handler.Handle(new LogoutCommand(userId, RawToken), CancellationToken.None);

        Assert.NotNull(token.RevokedAt);
        Assert.Null(token.ReplacedByTokenId);          // преемника нет: это logout, не ротация
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithForeignToken_DoesNothing()
    {
        var (handler, repository) = CreateSut();
        var token = NewToken(Guid.NewGuid());          // токен другого пользователя
        repository.GetByHashAsync(Hash, Arg.Any<CancellationToken>()).Returns(token);

        await handler.Handle(new LogoutCommand(Guid.NewGuid(), RawToken), CancellationToken.None);

        Assert.Null(token.RevokedAt);
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithAlreadyRevokedToken_DoesNothing()
    {
        var (handler, repository) = CreateSut();
        var userId = Guid.NewGuid();
        var token = NewToken(userId);
        token.Revoke();
        repository.GetByHashAsync(Hash, Arg.Any<CancellationToken>()).Returns(token);

        await handler.Handle(new LogoutCommand(userId, RawToken), CancellationToken.None);

        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithUnknownToken_DoesNothing()
    {
        var (handler, repository) = CreateSut();
        repository.GetByHashAsync(Hash, Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        await handler.Handle(new LogoutCommand(Guid.NewGuid(), RawToken), CancellationToken.None);

        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
