
using System;

namespace ProjectOrigin.WalletSystem.Server.Activities.Exceptions;

internal class InvalidRegistryTransactionException : Exception
{
    public InvalidRegistryTransactionException(string? message) : base(message)
    {
    }

    public InvalidRegistryTransactionException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
