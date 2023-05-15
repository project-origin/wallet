using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Database.Mapping;
using ProjectOrigin.Wallet.Server.HDWallet;
using ProjectOrigin.Wallet.Server.Services;
using System.IdentityModel.Tokens.Jwt;

namespace ProjectOrigin.Wallet.Server;

public class Startup
{
    private IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddGrpc();

        services.AddOptions<ServiceOptions>()
            .Bind(_configuration.GetSection("ServiceOptions"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateIssuerSigningKey = false,
                    ValidateAudience = false,
                    ValidateActor = false,
                    ValidateTokenReplay = false,
                    ValidateLifetime = false,
                    SignatureValidator = (token, _) => new JwtSecurityToken(token)
                };
            });
        services.AddAuthorization();

        services.AddScoped<UnitOfWork>();
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        services.AddSingleton<IHDAlgorithm, Secp256k1Algorithm>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<WalletService>();
            endpoints.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
        });

        app.ConfigureSqlMappers();
    }
}

