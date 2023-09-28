
using System;

namespace ProjectOrigin.WalletSystem.Server.Activities.Exceptions;

public class InvalidRegistryTransactionException : Exception
{
    public InvalidRegistryTransactionException(string? message) : base(message)
    {
    }

    public InvalidRegistryTransactionException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
