using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;

namespace Otus.PaymentSystem.Wallets.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Create_NegativeAmount_ThrowsDomainValidation()
    {
        var exception = Assert.Throws<DomainException>(() => Money.Create(-1m, Currency.Rub));

        Assert.Equal(DomainError.Validation, exception.Error);
    }

    [Fact]
    public void Create_MoreThanFourDecimals_Throws()
    {
        var exception = Assert.Throws<DomainException>(() => Money.Create(1.00001m, Currency.Rub));

        Assert.Equal(DomainError.Validation, exception.Error);
    }
}
