using System;

namespace ProjectOrigin.Vault.Activities.Exceptions;

public class ChroniclerException : Exception
{
    public ChroniclerException(string? message) : base(message)
    {
    }

    public ChroniclerException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
