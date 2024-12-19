using System;

namespace ProjectOrigin.Vault.Exceptions;

[Serializable]
public class QuantityNotYetAvailableToReserveException : Exception
{
    public QuantityNotYetAvailableToReserveException(string? message) : base(message)
    {
    }
}
