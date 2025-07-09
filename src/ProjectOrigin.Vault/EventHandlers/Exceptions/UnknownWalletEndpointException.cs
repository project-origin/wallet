using System;

namespace ProjectOrigin.Vault.EventHandlers.Exceptions;

public class UnknownWalletEndpointException : Exception
{
    public UnknownWalletEndpointException(string? message) : base(message)
    {
    }

    public UnknownWalletEndpointException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
