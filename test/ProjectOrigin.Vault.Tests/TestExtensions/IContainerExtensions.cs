using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;

namespace ProjectOrigin.Vault.Tests.TestExtensions;

public static class IContainerExtensions
{
    public static async Task StartWithLoggingAsync(this IContainer a)
    {
        try
        {
            await a.StartAsync()
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            var log = await a.GetLogsAsync();
            throw new Exception($"Container failed to start. Logs: {log}", e);
        }
    }
}
