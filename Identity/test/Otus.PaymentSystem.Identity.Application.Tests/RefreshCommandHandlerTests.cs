using Otus.PaymentSystem.Identity.Application.Commands.Auth;
using Otus.PaymentSystem.Identity.Application.Commands.Refresh;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Application.Tests;

public class RefreshCommandHandlerTests
{
    [Fact]
    public async Task Refresh_WithActiveToken_ReturnsNewTokensAndRevokesOld()
    {
        // 1. Подготовка: подставные зависимости (Substitute = «ненастоящий» объект).
        var factory = Substitute.For<IRefreshTokenFactory>();
        var repository = Substitute.For<IRefreshTokenRepository>();
        var users = Substitute.For<IUserRepository>();
        var tokens = Substitute.For<ITokenService>();

        var user = User.Register(new Email("user@example.com"), "stored-hash");
        var oldToken = RefreshToken.Issues(user.Id, "hash-old", DateTimeOffset.UtcNow.AddDays(30));

        // Что должен ответить каждый «ненастоящий» объект:
        factory.Hash("raw-old").Returns("hash-old");                    // клиент прислал raw-old
        factory.Issue().Returns(new IssuedRefreshToken(                 // выпускаем новый
            "raw-new", "hash-new", DateTimeOffset.UtcNow.AddDays(30)));
        repository.GetByHashAsync("hash-old", Arg.Any<CancellationToken>())
            .Returns(oldToken);                                          // нашли старый токен
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        tokens.GenerateToken(user).Returns(new TokenResult("jwt-new", DateTimeOffset.UtcNow.AddMinutes(15)));

        var handler = new RefreshCommandHandler(factory, repository, users, tokens);

        // 2. Действие: то, что делает эндпоинт.
        AuthTokens result = await handler.Handle(new RefreshCommand("raw-old"), CancellationToken.None);

        // 3. Проверка: новые токены пришли, старый отозван, новый сохранён.
        Assert.Equal("jwt-new", result.AccessToken);
        Assert.Equal("raw-new", result.RefreshToken);
        Assert.NotNull(oldToken.RevokedAt);
        await repository.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_WithRotatedToken_RevokesChainAndThrows()
    {
        var factory = Substitute.For<IRefreshTokenFactory>();
        var repository = Substitute.For<IRefreshTokenRepository>();
        var users = Substitute.For<IUserRepository>();
        var tokens = Substitute.For<ITokenService>();

        var user = User.Register(new Email("user@example.com"), "stored-hash");
        var head = RefreshToken.Issues(user.Id, "hash-head", DateTimeOffset.UtcNow.AddDays(30));
        var reused = RefreshToken.Issues(user.Id, "hash-old", DateTimeOffset.UtcNow.AddDays(30));
        reused.Revoke(head.Id);                       // старая ротация: у токена есть преемник

        factory.Hash("raw-old").Returns("hash-old");
        repository.GetByHashAsync("hash-old", Arg.Any<CancellationToken>()).Returns(reused);
        repository.GetByIdAsync(head.Id, Arg.Any<CancellationToken>()).Returns(head);

        var handler = new RefreshCommandHandler(factory, repository, users, tokens);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new RefreshCommand("raw-old"), CancellationToken.None));

        Assert.NotNull(head.RevokedAt);
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await users.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
