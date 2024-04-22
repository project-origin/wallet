using System;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.Configuration;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Database.Postgres;
using ProjectOrigin.WalletSystem.Server.Options;
using Serilog;

namespace ProjectOrigin.WalletSystem.Server.Extensions;

public static class IServiceCollectionExtensions
{
    public static void ConfigurePersistance(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IRepositoryUpgrader, PostgresUpgrader>();
        services.AddOptions<PostgresOptions>()
            .Configure(x => x.ConnectionString = configuration.GetConnectionString("Database")
                ?? throw new InvalidConfigurationException("Configuration does not contain a connection string named 'Database'."))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    public static void ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {

        var authOptions = configuration.GetSection("auth").GetValid<AuthOptions>();
        switch (authOptions.Type)
        {
            case AuthType.Jwt:
                JsonWebTokenHandler.DefaultInboundClaimTypeMap["scope"] = "http://schemas.microsoft.com/identity/claims/scope";
                var jwtOptions = authOptions.Jwt!;
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    o.ConfigureJwtVerification(jwtOptions);
                });

                services.AddAuthorization();
                if (jwtOptions.EnableScopeValidation)
                {
                    services.AddRequiredScopeAuthorization();
                }
                break;
            case AuthType.Header:
                Log.Warning("Authenticated user set based on header. Ensure that the header is set by a trusted source");
                services.AddAuthentication()
                    .AddScheme<HeaderAuthenticationHandlerOptions, HeaderAuthenticationHandler>("HeaderScheme", opts =>
                    {
                        opts.HeaderName = authOptions.Header!.HeaderName;
                    });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(authOptions.Type));
        }

    }
}
