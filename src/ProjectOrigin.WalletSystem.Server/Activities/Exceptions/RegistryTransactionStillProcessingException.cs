
using System;

namespace ProjectOrigin.WalletSystem.Server.Activities.Exceptions;

public class RegistryTransactionStillProcessingException : Exception
{
    public RegistryTransactionStillProcessingException(string? message) : base(message)
    {
    }
    public RegistryTransactionStillProcessingException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
