using System;
using System.Net;
using DotNet.Testcontainers.Configurations;

namespace ProjectOrigin.Vault.Tests.TestExtensions;

public static class IWaitForContainerOSExtensions
{
    public static IWaitForContainerOS UntilGrpcResponds(this IWaitForContainerOS waitForContainer, ushort grpcPort, Action<IWaitStrategy> waitStrategyModifier = null)
        => waitForContainer.UntilHttpRequestIsSucceeded(s => s.ForPath("/")
            .ForPort(grpcPort)
            .ForStatusCode(HttpStatusCode.BadRequest)
            .ForResponseMessageMatching(async r =>
                {
                    var content = await r.Content.ReadAsStringAsync();
                    var isHttp2ServerReady = "An HTTP/1.x request was sent to an HTTP/2 only endpoint.".Equals(content);
                    return isHttp2ServerReady;
                }
            ), waitStrategyModifier);
}
