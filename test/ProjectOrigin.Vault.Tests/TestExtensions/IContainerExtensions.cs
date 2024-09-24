using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;

namespace ProjectOrigin.Vault.Tests.TestExtensions;

public static class IContainerExtensions
{
    public static async Task StartWithLoggingAsync(this IContainer container)
    {
        try
        {
            await container.StartAsync()
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            var log = await container.GetLogsAsync();
            throw new Exception($"Container failed to start. Logs: {log}", e);
        }
    }
}
