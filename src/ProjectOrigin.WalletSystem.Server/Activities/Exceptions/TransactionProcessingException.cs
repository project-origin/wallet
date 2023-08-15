
using System;

namespace ProjectOrigin.WalletSystem.Server.Activities.Exceptions;

public class TransactionProcessingException : Exception
{
    public TransactionProcessingException(string? message) : base(message)
    {
    }
    public TransactionProcessingException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
