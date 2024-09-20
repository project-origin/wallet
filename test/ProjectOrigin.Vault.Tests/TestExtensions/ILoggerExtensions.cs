using System;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ProjectOrigin.Vault.Tests.TestExtensions;

public static class LoggerExtensions
{
    public static void CheckWarning(this ILogger logger, string message)
    {
        CheckWarning(logger, null, message);
    }

    public static void CheckWarning(this ILogger logger, Exception? exception, string message)
    {
        CheckLoggerMessage(logger, LogLevel.Warning, message, exception);

    }

    public static void CheckError(this ILogger logger, string message)
    {
        CheckError(logger, null, message);
    }

    public static void CheckError(this ILogger logger, Exception? exception, string message)
    {
        CheckLoggerMessage(logger, LogLevel.Error, message, exception);

    }

    private static void CheckLoggerMessage(this ILogger logger, LogLevel level, string message, Exception? exception = null)
    {
        logger.Log(
            level,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString() == message),
            exception,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
