using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Database;

var builder = WebApplication.CreateBuilder(args);

var startup = new Startup(builder.Configuration);
startup.ConfigureServices(builder.Services);

var app = builder.Build();
startup.Configure(app, builder.Environment);

if (args.Contains("--migrate"))
    DatabaseUpgrader.Upgrade(app.Configuration.GetConnectionString("Database"));

if (args.Contains("--serve"))
{
    if (!DatabaseUpgrader.IsUpgradeRequired(app.Configuration.GetConnectionString("Database")))
        throw new SystemException("Database is not up to date. Please run with --migrate first.");

    app.Run();
}
