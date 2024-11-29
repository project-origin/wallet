using System;

namespace ProjectOrigin.Vault.Exceptions;
public class QuantityNotYetAvailableToReserveException : Exception
{
    public QuantityNotYetAvailableToReserveException(string? message) : base(message)
    {
    }
}
