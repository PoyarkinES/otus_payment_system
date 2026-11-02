using FluentValidation.Results;
using Otus.PaymentSystem.Identity.Host.Contracts.Auth;
using Otus.PaymentSystem.Identity.Host.Contracts.Users;

namespace Otus.PaymentSystem.Identity.Host.Tests;

public class AuthRequestValidatorTests
{
    private readonly RegisterRequestValidator _register = new();
    private readonly UpdateProfileRequestValidator _profile = new();

    [Theory]
    [InlineData("")]
    [InlineData("bad@")]
    public void Register_InvalidEmail_Fails(string email)
    {
        ValidationResult result = _register.Validate(new RegisterRequest(email, "Password1"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Email));
    }

    [Fact]
    public void Register_EmailTooLong_Fails()
    {
        var email = new string('a', 316) + "@x.io";

        ValidationResult result = _register.Validate(new RegisterRequest(email, "Password1"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Email));
    }

    [Theory]
    [InlineData("Ab1")]        // короче 8
    [InlineData("12345678")]   // нет буквы
    [InlineData("abcdefgh")]   // нет цифры
    public void Register_WeakPassword_Fails(string password)
    {
        ValidationResult result = _register.Validate(new RegisterRequest("a@b.c", password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void Register_PasswordTooLong_Fails()
    {
        var password = new string('a', 127) + "A1"; // 129

        ValidationResult result = _register.Validate(new RegisterRequest("a@b.c", password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void Register_ValidRequest_Succeeds()
    {
        ValidationResult result = _register.Validate(new RegisterRequest("a@b.c", "Password1"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateProfile_EmptyRequest_IsValid()
    {
        ValidationResult result = _profile.Validate(new UpdateProfileRequest(null, null));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abcdefgh")]
    public void UpdateProfile_WeakPassword_Fails(string password)
    {
        ValidationResult result = _profile.Validate(new UpdateProfileRequest(null, password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProfileRequest.Password));
    }
}
