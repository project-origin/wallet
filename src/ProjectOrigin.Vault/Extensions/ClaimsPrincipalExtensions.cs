using System.Security.Claims;
using Grpc.Core;

namespace ProjectOrigin.Vault.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetSubject(this ServerCallContext context)
    {
        var principal = context.GetHttpContext().User;
        if (principal.TryGetSubject(out var subject))
        {
            return subject;
        }
        else
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Subject not found on claims principal"));
        }
    }

    public static bool TryGetSubject(this ClaimsPrincipal principal, out string subject)
    {
        subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return subject is not null;
    }
}
