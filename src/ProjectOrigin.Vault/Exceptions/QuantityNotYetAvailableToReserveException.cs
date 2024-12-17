using System;
using System.ComponentModel;

namespace ProjectOrigin.Vault.Exceptions;

[Serializable]
public class QuantityNotYetAvailableToReserveException : WarningException
{
    public QuantityNotYetAvailableToReserveException(string? message) : base(message)
    {
    }
}
