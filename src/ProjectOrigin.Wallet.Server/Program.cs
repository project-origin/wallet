using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using ProjectOrigin.Wallet.Server;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var startup = new Startup(builder.Configuration);

startup.ConfigureServices(builder.Services);

var app = builder.Build();

startup.Configure(app, builder.Environment);

DatabaseUpgrader.Upgrade(app.Configuration.GetConnectionString("Database"));

// Configure the HTTP request pipeline.
app.MapGrpcService<WalletService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
