using Otus.PaymentSystem.Identity.Application.Commands.Login;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Application.Tests;

/// <summary>
/// Тесты <see cref="LoginCommandHandler"/>: проверяют оркестрацию и,
/// в частности, порядок проверок (user enumeration).
/// </summary>
public class LoginCommandHandlerTests
{
    private const string ValidEmail = "user@example.com";
    private const string ValidPassword = "P@ssw0rd";

    private static (LoginCommandHandler Handler, IUserRepository Users, IPasswordHasher Hasher, ITokenService Tokens) CreateSut()
    {
        var users = Substitute.For<IUserRepository>();
        var hasher = Substitute.For<IPasswordHasher>();
        var tokens = Substitute.For<ITokenService>();
        return (new LoginCommandHandler(users, hasher, tokens), users, hasher, tokens);
    }

    private static User CreateActiveUser()
        => User.Register(new Email(ValidEmail), "stored-hash");

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        var (handler, users, hasher, tokens) = CreateSut();
        var user = CreateActiveUser();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        hasher.Verify(ValidPassword, user.PasswordHash).Returns(true);
        tokens.GenerateToken(user).Returns(new TokenResult("jwt-token", expiresAt));

        var result = await handler.Handle(new LoginCommand(ValidEmail, ValidPassword), CancellationToken.None);

        Assert.Equal("jwt-token", result.AccessToken);
        Assert.Equal(expiresAt, result.ExpiresAt);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ThrowsDomainException_WithGenericMessage()
    {
        var (handler, users, _, _) = CreateSut();
        users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new LoginCommand(ValidEmail, ValidPassword), CancellationToken.None));

        Assert.Equal("Неверные учётные данные.", ex.Message);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsDomainException_WithGenericMessage()
    {
        var (handler, users, hasher, _) = CreateSut();
        var user = CreateActiveUser();
        users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        hasher.Verify(ValidPassword, user.PasswordHash).Returns(false);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new LoginCommand(ValidEmail, ValidPassword), CancellationToken.None));

        // Ключевой момент безопасности: сообщение такое же, как при "unknown email".
        Assert.Equal("Неверные учётные данные.", ex.Message);
    }

    [Fact]
    public async Task Login_WithBlockedUser_ThrowsDomainException_OnlyAfterPasswordVerified()
    {
        var (handler, users, hasher, _) = CreateSut();
        var user = CreateActiveUser();
        user.Block(Guid.NewGuid());

        users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        hasher.Verify(ValidPassword, user.PasswordHash).Returns(true);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new LoginCommand(ValidEmail, ValidPassword), CancellationToken.None));

        Assert.Contains("заблокирован", ex.Message);
        hasher.Received(1).Verify(ValidPassword, user.PasswordHash);
    }

    [Fact]
    public async Task Login_WithBlockedUser_AndWrongPassword_DoesNotRevealBlockStatus()
    {
        var (handler, users, hasher, _) = CreateSut();
        var user = CreateActiveUser();
        user.Block(Guid.NewGuid());

        users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        hasher.Verify(ValidPassword, user.PasswordHash).Returns(false);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new LoginCommand(ValidEmail, ValidPassword), CancellationToken.None));

        // Если пароль неверный, мы НЕ говорим, что аккаунт заблокирован.
        Assert.Equal("Неверные учётные данные.", ex.Message);
    }

    [Fact]
    public async Task Login_WithInvalidEmail_ThrowsDomainException()
    {
        var (handler, _, _, _) = CreateSut();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new LoginCommand("not-an-email", ValidPassword), CancellationToken.None));
    }
}
