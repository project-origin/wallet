using System;
using System.Reflection;
using DbUp;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ProjectOrigin.Wallet.Server;
using ProjectOrigin.Wallet.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
builder.Services.AddGrpc();

builder.Services.AddHostedService<SomeBackgroundService>();

var app = builder.Build();

var upgrader = DeployChanges.To
    .PostgresqlDatabase("Host=localhost; Port=5432; Database=postgres; Username=admin; Password=admin;")
    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
    .LogToConsole() //TODO: Proper logging
    .Build();

var databaseUpgradeResult = upgrader.PerformUpgrade();
if (!databaseUpgradeResult.Successful)
{
    //TODO: Proper logging
    Console.WriteLine(databaseUpgradeResult.Error);
    throw databaseUpgradeResult.Error;
}

// Configure the HTTP request pipeline.
app.MapGrpcService<WalletService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
