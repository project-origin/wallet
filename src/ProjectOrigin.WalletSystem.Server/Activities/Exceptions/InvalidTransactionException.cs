
using System;

namespace ProjectOrigin.WalletSystem.Server.Activities.Exceptions;

internal class InvalidTransactionException : Exception
{
    public InvalidTransactionException(string? message) : base(message)
    {
    }

    public InvalidTransactionException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
