using System.Security.Claims;
using Grpc.Core;

public static class ClaimsPrincipalExtensions
{
    public static string GetSubject(this ServerCallContext context)
    {
        return context.GetHttpContext().User.GetSubject();
    }

    public static string GetSubject(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new RpcException(new Status(StatusCode.Unauthenticated, "Subject not found on claims principal"));
    }
}
