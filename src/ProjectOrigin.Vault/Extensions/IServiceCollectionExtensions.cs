using System;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.Configuration;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Database.Postgres;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Services.REST;
using Serilog;

namespace ProjectOrigin.Vault.Extensions;

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

    public static void ConfigureAuthentication(this IServiceCollection services, AuthOptions authOptions)
    {

        switch (authOptions.Type)
        {
            case AuthType.Jwt:
                var jwtOptions = authOptions.Jwt!;
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    o.ConfigureJwtVerification(jwtOptions);
                });

                services.AddAuthorization();
                if (jwtOptions.EnableScopeValidation)
                {
                    JsonWebTokenHandler.DefaultInboundClaimTypeMap["scope"] = "http://schemas.microsoft.com/identity/claims/scope";
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
                throw new NotSupportedException(nameof(authOptions.Type));
        }
    }

    public static void ConfigureOtlp(this IServiceCollection services, OtlpOptions otlpOptions)
    {
        var assemblyName = Assembly.GetEntryAssembly()?.FullName ?? throw new InvalidOperationException("Entry assembly name not found");

        if (otlpOptions.Enabled)
        {
            services.AddOpenTelemetry()
                .ConfigureResource(r =>
                {
                    r.AddService(assemblyName, serviceInstanceId: Environment.MachineName);
                })
                .WithMetrics(metrics => metrics
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddMeter(MassTransit.Monitoring.InstrumentationOptions.MeterName)
                    .AddMeter(MeterBase.MeterName)
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = otlpOptions.Endpoint!))
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
                        .AddSource(MassTransit.Logging.DiagnosticHeaders.DefaultListenerName)
                        .AddOtlpExporter(o => o.Endpoint = otlpOptions.Endpoint!));
        }
    }
}
