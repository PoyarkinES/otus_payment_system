using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.Errors;

namespace Otus.PaymentSystem.Wallets.Application.Exceptions
{
    public sealed class DuplicateHoldException : DomainException
    {
        public DuplicateHoldException() : base("Холд с таким TransferId уже существует.", DomainError.Conflict)
        {
        }

        public DuplicateHoldException(string message, DomainError error = DomainError.Conflict) : base(message, error)
        {
        }

        public DuplicateHoldException(string message, Exception innerException, DomainError error = DomainError.Conflict) : base(message, innerException, error)
        {
        }
    }
}
