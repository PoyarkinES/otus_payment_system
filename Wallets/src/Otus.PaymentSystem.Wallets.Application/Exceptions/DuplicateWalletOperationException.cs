namespace Otus.PaymentSystem.Wallets.Application.Exceptions;

public sealed class DuplicateWalletOperationException() : Exception("Операция с таким Idempotency-Key уже выполнена.");
