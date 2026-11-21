using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.Errors;

namespace Otus.PaymentSystem.Wallets.Application.Exceptions
{
    public sealed class DuplicateWalletException : DomainException
    {
        public DuplicateWalletException() : base("Кошелёк с такой валютой уже существует.", DomainError.Conflict)
        {
        }

        public DuplicateWalletException(string message, DomainError error = DomainError.Conflict) : base(message, error)
        {
        }

        public DuplicateWalletException(string message, Exception innerException, DomainError error = DomainError.Conflict) : base(message, innerException, error)
        {
        }
    }
}
