namespace Otus.PaymentSystem.Wallets.Application.Exceptions;

public sealed class DuplicateWalletOperationException : Exception
{
    public DuplicateWalletOperationException()
        : base("Операция с таким Idempotency-Key уже выполнена.")
    {
    }
}
