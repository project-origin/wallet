using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
using ProjectOrigin.WalletSystem.Server.Serialization;
using ProjectOrigin.WalletSystem.Server.Services;
using ProjectOrigin.WalletSystem.Server.Services.REST;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MassTransit.Logging;
using MassTransit.Monitoring;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;

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

        var algorithm = new Secp256k1Algorithm();
        services.AddSingleton<IHDAlgorithm>(algorithm);

        services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                o.JsonSerializerOptions.Converters.Add(new IHDPublicKeyConverter(algorithm));
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

        services.AddOptions<OtlpOptions>()
            .BindConfiguration(OtlpOptions.Prefix)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.ConfigurePersistance(_configuration);

        JsonWebTokenHandler.DefaultInboundClaimTypeMap["scope"] = "http://schemas.microsoft.com/identity/claims/scope";
        var jwtOptions = _configuration.GetSection("jwt").GetValid<JwtOptions>();
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

        void ConfigureResource(ResourceBuilder r)
        {
            r.AddService("ProjectOrigin.WalletSystem.Server",
                serviceInstanceId: Environment.MachineName);
        }

        var otlpOptions = _configuration.GetSection(OtlpOptions.Prefix).GetValid<OtlpOptions>();
        if (otlpOptions.Enabled)
        {
            services.AddOpenTelemetry()
                .ConfigureResource(ConfigureResource)
                .WithMetrics(metrics => metrics
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddMeter(InstrumentationOptions.MeterName)
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = otlpOptions.Endpoint))
                .WithTracing(provider =>
                    provider
                        .AddGrpcClientInstrumentation(grpcOptions =>
                        {
                            grpcOptions.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                                activity.SetTag("requestVersion", httpRequestMessage.Version);
                            grpcOptions.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                                activity.SetTag("responseVersion", httpResponseMessage.Version);
                            grpcOptions.SuppressDownstreamInstrumentation = true;
                        })
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation()
                        .AddNpgsql()
                        .AddSource(DiagnosticHeaders.DefaultListenerName)
                        .AddOtlpExporter(o => o.Endpoint = otlpOptions.Endpoint));
        }

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

        services.AddSwaggerGen(options =>
        {
            options.EnableAnnotations();
            options.SchemaFilter<IHDPublicKeySchemaFilter>();
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            options.IncludeXmlComments(xmlPath);
            options.DocumentFilter<AddWalletTagDocumentFilter>();
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
            endpoints.MapControllers();
        });

        app.ConfigureSqlMappers();
    }
}
