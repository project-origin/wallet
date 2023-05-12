using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Services;

namespace ProjectOrigin.Wallet.Server;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddGrpc();

        services.AddScoped<UnitOfWork>();
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<WalletService>();
            endpoints.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
        });
    }
}
