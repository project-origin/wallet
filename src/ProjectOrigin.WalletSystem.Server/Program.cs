using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var startup = new Startup(builder.Configuration);

startup.ConfigureServices(builder.Services);

var app = builder.Build();

startup.Configure(app, builder.Environment);

DatabaseUpgrader.Upgrade(app.Configuration.GetConnectionString("Database"));

app.Run();
