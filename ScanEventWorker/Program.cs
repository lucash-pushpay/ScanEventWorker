using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LiteDB;
using NodaTime;
using Serilog;
using Scan_Event_NoSQL.Services;
using Scan_Event_NoSQL.Repositories;

// Create Serilog logger first for early logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = Host.CreateApplicationBuilder(args);

// Add configuration sources - this is crucial for console apps
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Configure Serilog from configuration
builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

// Configure NodaTime BSON type registration for LiteDB
BsonMapper.Global.RegisterType
(
    serialize: (Instant instant) => instant.ToDateTimeUtc(),
    deserialize: (BsonValue bson) => Instant.FromDateTimeUtc(DateTime.SpecifyKind(bson.AsDateTime, DateTimeKind.Utc))
);
BsonMapper.Global.RegisterType
(
    serialize: (Instant? instant) => instant?.ToDateTimeUtc(),
    deserialize: (BsonValue bson) =>
        bson.IsNull ? null : Instant.FromDateTimeUtc(DateTime.SpecifyKind(bson.AsDateTime, DateTimeKind.Utc))
);

// Configure LiteDB database
builder.Services.AddSingleton<LiteDatabase>(_ =>
{
    var connectionString = builder.Configuration["Database:ConnectionString"] ?? "scanevents.db";
    return new LiteDatabase(connectionString);
});
// Register caching
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICacheService, MemoryCacheService>();

// Register repositories
builder.Services.AddSingleton<IParcelScanRepository, ParcelScanRepository>();
builder.Services.AddSingleton<IScanEventRepository, ScanEventRepository>();
builder.Services.AddSingleton<IProcessingStateRepository, ProcessingStateRepository>();

// Register transaction service
builder.Services.AddSingleton<ILiteDbTransactionService, LiteDbTransactionService>();

// Register business logic services
builder.Services.AddSingleton<IScanEventProcessingService, ScanEventProcessingService>();

// Configure HTTP client with basic timeout configuration
builder.Services.AddHttpClient<IScanEventApiClient, ScanEventApiClientWithCache>(client =>
{
    var baseUrl = builder.Configuration["ScanEventApi:BaseUrl"] ?? "http://localhost/";
    var timeoutSeconds = builder.Configuration.GetValue("ScanEventApi:TimeoutSeconds", 30);

    // Validate configuration
    if (baseUrl == "http://localhost/")
    {
        Log.Warning("Using default BaseUrl. Check if appsettings.json is properly configured!");
    }

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    client.DefaultRequestHeaders.Add("User-Agent", "ScanEventWorker/1.0");

    Log.Information("HTTP Client configured with BaseAddress: {BaseAddress}", client.BaseAddress);
});

// Register the worker service
builder.Services.AddHostedService<ScanEventWorkerService>();

var host = builder.Build();

// Log startup information using Serilog directly
Log.Information("ScanEventWorker Worker starting up...");
Log.Information("API Base URL: {BaseUrl}", builder.Configuration["ScanEventApi:BaseUrl"]);
Log.Information("Database: {Database}", builder.Configuration["Database:ConnectionString"]);

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("ScanEventWorker Worker shutting down...");
    await Log.CloseAndFlushAsync();
}