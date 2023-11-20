using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.Activities;
using ProjectOrigin.WalletSystem.Server.Activities.Exceptions;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
using ProjectOrigin.WalletSystem.Server.Database.Postgres;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Options;
using ProjectOrigin.WalletSystem.Server.Projections;
using ProjectOrigin.WalletSystem.Server.Services;
using ProjectOrigin.WalletSystem.Server.Services.GRPC;
using ProjectOrigin.WalletSystem.Server.Services.REST;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectOrigin.WalletSystem.Server;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddGrpc();

        services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            });

        services.AddSwaggerGen(o =>
        {
            o.SupportNonNullableReferenceTypes();
            o.DocumentFilter<PathBaseDocumentFilter>();
        });

        services.AddTransient<IStreamProjector<GranularCertificate>, GranularCertificateProjector>();
        services.AddTransient<IRegistryProcessBuilderFactory, RegistryProcessBuilderFactory>();
        services.AddTransient<IRegistryService, RegistryService>();

        services.AddOptions<ServiceOptions>()
            .Bind(_configuration.GetSection("ServiceOptions"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RegistryOptions>()
            .Bind(_configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RestApiOptions>()
            .Bind(_configuration.GetSection("RestApiOptions"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.ConfigurePersistance(_configuration);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                var jwtOptions = _configuration.GetSection("jwt").GetValid<JwtOptions>();

                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = !jwtOptions.Audience.IsEmpty(),
                    TryAllIssuerSigningKeys = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidateTokenReplay = false,
                    RequireSignedTokens = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidIssuers = jwtOptions.Issuers.Select(x => x.IssuerName).ToList(),
                    IssuerSigningKeys = jwtOptions.Issuers.Select(x => x.SecurityKey).ToList(),
                };
            });
        services.AddAuthorization();

        services.AddMassTransit(o =>
        {
            o.SetKebabCaseEndpointNameFormatter();

            o.AddConsumer<TransferCertificateCommandHandler>(cfg =>
            {
                cfg.UseMessageRetry(r => r.Interval(100, TimeSpan.FromMinutes(1))
                    .Handle<TransientException>());
            });

            o.AddConsumer<VerifySliceCommandHandler>(cfg =>
            {
                cfg.UseMessageRetry(r => r.Interval(100, TimeSpan.FromMinutes(1))
                    .Handle<TransientException>());
            });

            o.AddConsumer<ClaimCertificateCommandHandler>(cfg =>
            {
                cfg.UseMessageRetry(r => r.Interval(100, TimeSpan.FromMinutes(1))
                    .Handle<TransientException>());
            });

            o.AddActivitiesFromNamespaceContaining<TransferFullSliceActivity>();
            o.AddExecuteActivity<WaitCommittedRegistryTransactionActivity, WaitCommittedTransactionArguments>(cfg =>
            {
                cfg.UseRetry(r => r.Interval(100, TimeSpan.FromSeconds(10))
                    .Handle<RegistryTransactionStillProcessingException>());
            });

            o.ConfigureMassTransitTransport(_configuration.GetSection("MessageBroker").GetValid<MessageBrokerOptions>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IDbConnectionFactory, PostgresConnectionFactory>();
        services.AddSingleton<IHDAlgorithm, Secp256k1Algorithm>();

        services.AddSwaggerGen(options =>
        {
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            options.IncludeXmlComments(xmlPath);
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        var pathBase = app.ApplicationServices.GetRequiredService<IOptions<RestApiOptions>>().Value.PathBase;
        app.UsePathBase(pathBase);

        app.UseSwagger();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<WalletService>();
            endpoints.MapGrpcService<ReceiveSliceService>();
            endpoints.MapControllers();
        });

        app.ConfigureSqlMappers();
    }
}
