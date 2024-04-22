using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ProjectOrigin.WalletSystem.Server.Services.REST;

public class CustomClaimsMiddleware
{
    private readonly RequestDelegate _next;

    public CustomClaimsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("YourHeaderName", out var headerValue))
        {
            var claimsIdentity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, headerValue.ToString()) });
            context.User = new ClaimsPrincipal(claimsIdentity);
        }

        await _next(context);
    }
}
