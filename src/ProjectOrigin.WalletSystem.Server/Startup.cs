using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
using ProjectOrigin.WalletSystem.Server.Services;
using System.IdentityModel.Tokens.Jwt;
using MassTransit;
using ProjectOrigin.WalletSystem.Server.BackgroundJobs;
using ProjectOrigin.WalletSystem.Server.Projections;
using ProjectOrigin.WalletSystem.Server.Options;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;

namespace ProjectOrigin.WalletSystem.Server;

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

        services.AddTransient<IStreamProjector<GranularCertificate>, GranularCertificateProjector>();
        services.AddTransient<IRegistryService, RegistryService>();

        services.AddOptions<ServiceOptions>()
            .Bind(_configuration.GetSection("ServiceOptions"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RegistryOptions>()
            .Bind(_configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<VerifySlicesWorkerOptions>(
            _configuration.GetSection("VerifySlicesWorkerOptions"));

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

        services.AddMassTransit(o =>
        {
            o.SetKebabCaseEndpointNameFormatter();

            o.AddConsumer<TransferCertificateCommandHandler>();

            o.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<UnitOfWork>();
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();

        services.AddSingleton<IHDAlgorithm, Secp256k1Algorithm>();

        services.AddHostedService<VerifySlicesWorker>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<WalletService>();
            endpoints.MapGrpcService<ReceiveSliceService>();
            endpoints.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
        });

        app.ConfigureSqlMappers();
    }
}

