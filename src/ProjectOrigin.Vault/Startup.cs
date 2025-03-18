using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.ServiceCommon.UriOptionsLoader;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.Activities.Exceptions;
using ProjectOrigin.Vault.CommandHandlers;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Database.Mapping;
using ProjectOrigin.Vault.Database.Postgres;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Projections;
using ProjectOrigin.Vault.Serialization;
using ProjectOrigin.Vault.Services;
using ProjectOrigin.Vault.Services.REST;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Jobs;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Middleware;

namespace ProjectOrigin.Vault;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        var algorithm = new Secp256k1Algorithm();
        services.AddSingleton<IHDAlgorithm>(algorithm);

        services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                o.JsonSerializerOptions.Converters.Add(new IHDPublicKeyConverter(algorithm));
            });

        services.AddTransient<IStreamProjector<GranularCertificate>, GranularCertificateProjector>();
        services.AddTransient<IRegistryProcessBuilderFactory, RegistryProcessBuilderFactory>();
        services.AddTransient<IRegistryService, RegistryService>();

        services.AddOptions<ServiceOptions>()
            .Bind(_configuration.GetSection("ServiceOptions"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<OtlpOptions>()
            .BindConfiguration(OtlpOptions.Prefix)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RetryOptions>()
            .BindConfiguration(RetryOptions.Retry)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JobOptions>()
            .BindConfiguration(JobOptions.Job)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.ConfigurePersistance(_configuration);
        services.ConfigureAuthentication(_configuration.GetValidSection<AuthOptions>(AuthOptions.Prefix));
        services.AddSingleton<MeterBase>();
        services.AddSingleton<IClaimMetrics, ClaimMetrics>();
        services.AddSingleton<ITransferMetrics, TransferMetrics>();

        services.ConfigureOtlp(_configuration.GetValidSection<OtlpOptions>(OtlpOptions.Prefix));

        services.AddMassTransit(o =>
        {
            o.SetKebabCaseEndpointNameFormatter();
            var options = _configuration.GetSection("MessageBroker").GetValid<MessageBrokerOptions>();
            if (options.RabbitMq != null && options.RabbitMq.Quorum)
            {
                o.AddConfigureEndpointsCallback((name, cfg) =>
                {
                    if (cfg is IRabbitMqReceiveEndpointConfigurator rmq)
                        rmq.SetQuorumQueue(options.RabbitMq.Replicas);

                    cfg.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(10))
                        .Handle<TransientException>());
                });
            }

            o.AddConsumer<VerifySliceCommandHandler>();

            o.AddConsumer<TransferCertificateCommandHandler>(cfg =>
            {
                cfg.UseMessageRetry(r => r.Interval(20, TimeSpan.FromSeconds(5))
                    .Handle<QuantityNotYetAvailableToReserveException>());
            });

            o.AddConsumer<ClaimCertificateCommandHandler>(cfg =>
            {
                cfg.UseMessageRetry(r => r.Interval(20, TimeSpan.FromSeconds(5))
                    .Handle<QuantityNotYetAvailableToReserveException>());
            });

            o.AddConsumer<CheckForWithdrawnCertificatesCommandHandler>();

            o.AddActivitiesFromNamespaceContaining<TransferFullSliceActivity>();
            o.AddExecuteActivity<WaitCommittedRegistryTransactionActivity, WaitCommittedTransactionArguments>((rc, cfg) =>
            {
                var retryOptions = rc.GetRequiredService<IOptions<RetryOptions>>();
                cfg.UseRetry(r => r.Incremental(retryOptions!.Value.RegistryTransactionStillProcessingRetryCount,
                        TimeSpan.FromSeconds(retryOptions.Value.RegistryTransactionStillProcessingInitialIntervalSeconds),
                        TimeSpan.FromSeconds(retryOptions.Value.RegistryTransactionStillProcessingIntervalIncrementSeconds))
                    .Handle<RegistryTransactionStillProcessingException>());
            });

            o.ConfigureMassTransitTransport(options);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IDbConnectionFactory, PostgresConnectionFactory>();

        services.AddSwaggerGen(options =>
        {
            options.SupportNonNullableReferenceTypes();
            options.EnableAnnotations();
            options.SchemaFilter<IHDPublicKeySchemaFilter>();
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            options.IncludeXmlComments(xmlPath);
            options.DocumentFilter<PathBaseDocumentFilter>();
            options.DocumentFilter<AddWalletTagDocumentFilter>();
        });

        services.AddHttpClient();
        services.ConfigureUriOptionsLoader<NetworkOptions>("network");

        services.AddHostedService<PublishCheckForWithdrawnCertificatesCommandJob>();
        services.AddHostedService<ExpireCertificatesJob>();
        services.AddHostedService<OutboxPollingWorker>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        PrintNetworkOptions(app);

        var pathBase = app.ApplicationServices.GetRequiredService<IOptions<ServiceOptions>>().Value.PathBase;
        app.UsePathBase(pathBase);

        app.UseSwagger();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseMiddleware<ExceptionLoggerMiddleware>();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });

        app.ConfigureSqlMappers();
    }

    private void PrintNetworkOptions(IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILogger<Startup>>();
        var options = app.ApplicationServices.GetRequiredService<IOptions<NetworkOptions>>().Value;
        logger.LogInformation(options.ToString());
    }
}
