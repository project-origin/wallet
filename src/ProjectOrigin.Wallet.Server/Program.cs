using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using ProjectOrigin.Wallet.Server;
using ProjectOrigin.Wallet.Server.Database;

var startup = new Startup();

var builder = WebApplication.CreateBuilder(args);

startup.ConfigureServices(builder.Services);

var app = builder.Build();

startup.Configure(app, builder.Environment);

DatabaseUpgrader.Upgrade(app.Configuration.GetConnectionString("Database"));

app.Run();
