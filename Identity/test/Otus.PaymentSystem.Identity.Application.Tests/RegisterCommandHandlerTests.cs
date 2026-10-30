using Otus.PaymentSystem.Identity.Application.Commands.Register;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Exceptions;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Application.Tests;

/// <summary>
/// Тесты <see cref="RegisterCommandHandler"/>: проверяют оркестрацию (проверка -> хеш -> сохранение),
/// не дублируя инварианты Domain (они покрыты в Domain.Tests).
/// </summary>
public class RegisterCommandHandlerTests
{
    private const string ValidEmail = "user@example.com";
    private const string ValidPassword = "P@ssw0rd";

    private static (RegisterCommandHandler Handler, IUserRepository Users, IPasswordHasher Hasher) CreateSut()
    {
        var users = Substitute.For<IUserRepository>();
        var hasher = Substitute.For<IPasswordHasher>();
        return (new RegisterCommandHandler(users, hasher), users, hasher);
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsResultAndSavesUser()
    {
        var (handler, users, hasher) = CreateSut();
        const string expectedHash = "hashed-password";

        users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
             .Returns((User?)null);
        hasher.Hash(ValidPassword).Returns(expectedHash);

        var result = await handler.Handle(new RegisterCommand(ValidEmail, ValidPassword), CancellationToken.None);

        Assert.Equal(ValidEmail, result.Email);
        Assert.NotEqual(Guid.Empty, result.UserId);
        await users.Received(1).AddAsync(
            Arg.Is<User>(u => u.Email.Value == ValidEmail && u.PasswordHash == expectedHash),
            Arg.Any<CancellationToken>());
        await users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_WithExistingEmail_ThrowsDomainException_AndDoesNotSave()
    {
        var (handler, users, _) = CreateSut();
        var existing = User.Register(new Email(ValidEmail), "some-hash");
        users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
             .Returns(existing);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new RegisterCommand(ValidEmail, ValidPassword), CancellationToken.None));

        Assert.Contains("занят", ex.Message);
        await users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await users.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ThrowsDomainException()
    {
        var (handler, _, _) = CreateSut();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new RegisterCommand("not-an-email", ValidPassword), CancellationToken.None));
    }
}
