using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Database;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);
var loggerConfiguration = new LoggerConfiguration()
    .Filter.ByExcluding("RequestPath like '/health%'")
    .Filter.ByExcluding("RequestPath like '/metrics%'")
    .Enrich.WithSpan();

loggerConfiguration = builder.Environment.IsDevelopment()
    ? loggerConfiguration.WriteTo.Console()
    : loggerConfiguration.WriteTo.Console(new JsonFormatter());

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(loggerConfiguration.CreateLogger());

var startup = new Startup(builder.Configuration);
startup.ConfigureServices(builder.Services);

var app = builder.Build();
startup.Configure(app, builder.Environment);

if (args.Contains("--migrate"))
{
    Console.WriteLine("Starting database migration.");
    await DatabaseUpgrader.Upgrade(app.Configuration.GetConnectionString("Database"));
    Console.WriteLine("Database migrated successfully.");
}

if (args.Contains("--serve"))
{
    Console.WriteLine("Starting server.");
    if (DatabaseUpgrader.IsUpgradeRequired(app.Configuration.GetConnectionString("Database")))
        throw new SystemException("Database is not up to date. Please run with --migrate first.");

    app.Run();
    Console.WriteLine("Server stopped.");
}
