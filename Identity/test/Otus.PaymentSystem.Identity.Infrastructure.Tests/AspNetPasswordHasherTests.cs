using Otus.PaymentSystem.Identity.Infrastructure.Security;

namespace Otus.PaymentSystem.Identity.Infrastructure.Tests;

public class AspNetPasswordHasherTests
{
    private readonly AspNetPasswordHasher _hasher = new(new Microsoft.AspNetCore.Identity.PasswordHasher<object>());

    [Fact]
    public void Hash_And_Verify_SamePassword_ReturnsTrue()
    {
        const string password = "P@ssw0rd";

        var hash = _hasher.Hash(password);

        Assert.True(_hasher.Verify(password, hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("P@ssw0rd");

        Assert.False(_hasher.Verify("wrong", hash));
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        const string password = "P@ssw0rd";

        var hash1 = _hasher.Hash(password);
        var hash2 = _hasher.Hash(password);

        // Соль внутри: одинаковые пароли -> разные хеши. Это правильно.
        Assert.NotEqual(hash1, hash2);
        Assert.True(_hasher.Verify(password, hash1));
        Assert.True(_hasher.Verify(password, hash2));
    }
}
