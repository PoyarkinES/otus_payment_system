using Otus.PaymentSystem.Wallets.Domain.Exceptions;
using Otus.PaymentSystem.Wallets.Domain.Errors;

namespace Otus.PaymentSystem.Wallets.Application.Exceptions
{
    public sealed class WalletConcurrencyException : DomainException
    {
        public WalletConcurrencyException() : base("Кошелёк был изменён другим запросом. Повторите операцию.", DomainError.Conflict)
        {
        }

        public WalletConcurrencyException(string message, DomainError error = DomainError.Conflict) : base(message, error)
        {
        }

        public WalletConcurrencyException(string message, Exception innerException, DomainError error = DomainError.Conflict) : base(message, innerException, error)
        {
        }
    }
}
