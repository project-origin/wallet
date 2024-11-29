using System;

namespace ProjectOrigin.Vault.Exceptions;
public class QuantityNotYetAvailableToReserveException : Exception
{
    public QuantityNotYetAvailableToReserveException(string? message) : base(message)
    {
    }

    public QuantityNotYetAvailableToReserveException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
