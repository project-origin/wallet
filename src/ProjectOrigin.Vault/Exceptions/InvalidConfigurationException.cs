using System;

namespace ProjectOrigin.Vault.Exceptions;

public class InvalidConfigurationException : Exception
{
    public InvalidConfigurationException(string? message) : base(message)
    {
    }

    public InvalidConfigurationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
