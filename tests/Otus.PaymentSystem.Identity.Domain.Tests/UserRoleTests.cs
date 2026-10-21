using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.ValueObjects;

namespace Otus.PaymentSystem.Identity.Domain.Tests;

public class UserRoleTests
{
    [Fact]
    public void Register_DefaultRole_IsUser()
    {
        var user = User.Register(new Email("user@example.com"), "hash");

        Assert.Equal(UserRole.User, user.Role);
    }

    [Fact]
    public void PromoteToAdmin_ChangesRole()
    {
        var user = User.Register(new Email("user@example.com"), "hash");

        user.PromoteToAdmin();

        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public void PromoteToAdmin_Twice_IsIdempotent()
    {
        var user = User.Register(new Email("user@example.com"), "hash");
        user.PromoteToAdmin();
        var updatedAt = user.UpdatedAt;

        user.PromoteToAdmin();

        Assert.Equal(UserRole.Admin, user.Role);
        Assert.Equal(updatedAt, user.UpdatedAt); // второй раз ничего не произошло
    }
}
