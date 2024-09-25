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
using Microsoft.IdentityModel.Protocols.Configuration;
using ProjectOrigin.Vault.Jobs;
using Quartz;

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

        services.ConfigurePersistance(_configuration);
        services.ConfigureAuthentication(_configuration.GetValidSection<AuthOptions>(AuthOptions.Prefix));
        services.ConfigureOtlp(_configuration.GetValidSection<OtlpOptions>(OtlpOptions.Prefix));

        services.AddQuartz(q =>
        {
            q.UsePersistentStore(s =>
            {
                s.UsePostgres(_configuration.GetConnectionString("Database") ?? throw new InvalidConfigurationException("Configuration does not contain a connection string named 'Database'."));
                s.UseNewtonsoftJsonSerializer();
                s.UseClustering(c =>
                {
                    c.CheckinInterval = TimeSpan.FromSeconds(20);  // Node health check interval
                    c.CheckinMisfireThreshold = TimeSpan.FromSeconds(60);  // Time to consider a node as misfired
                });
            });

            q.AddJob<PublishCheckForWithdrawnCertificatesCommandJob>(opts => opts.WithIdentity("PublishCheckForWithdrawnCertificatesCommandClusteredJob"));

            q.AddTrigger(opts => opts
                .ForJob("PublishCheckForWithdrawnCertificatesCommandClusteredJob")
                .WithIdentity("PublishCheckForWithdrawnCertificatesCommandClusteredJob-trigger")
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(60)
                    .RepeatForever()));
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

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
                });
            }

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

            o.AddConsumer<CheckForWithdrawnCertificatesCommandHandler>();

            o.AddActivitiesFromNamespaceContaining<TransferFullSliceActivity>();
            o.AddExecuteActivity<WaitCommittedRegistryTransactionActivity, WaitCommittedTransactionArguments>(cfg =>
            {
                cfg.UseRetry(r => r.Interval(100, TimeSpan.FromSeconds(10))
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
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        var pathBase = app.ApplicationServices.GetRequiredService<IOptions<ServiceOptions>>().Value.PathBase;
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
