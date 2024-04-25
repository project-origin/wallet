using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProjectOrigin.WalletSystem.Server.Services.REST;

public class HeaderAuthenticationHandler : AuthenticationHandler<HeaderAuthenticationHandlerOptions>
{

    public HeaderAuthenticationHandler(
        IOptionsMonitor<HeaderAuthenticationHandlerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.Request.Headers.TryGetValue(Options.HeaderName!, out var headerValue))
        {
            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, headerValue.ToString())
                };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Header"));
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        else
        {
            return Task.FromResult(AuthenticateResult.Fail("Header does not exist"));
        }
    }
}

public class HeaderAuthenticationHandlerOptions : AuthenticationSchemeOptions
{
    public string? HeaderName { get; set; }
}
