using Azure.Data.Tables;
using Azure.Monitor.OpenTelemetry.Exporter;
using EmailService.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}
// 1. Read storage connection string from builder.Configuration (local.settings.json or app settings)
var storageConnectionString = builder.Configuration["AzureWebJobsStorage"]
    ?? throw new InvalidOperationException("Missing 'AzureWebJobsStorage' connection string.");

// 2. Register TableServiceClient
builder.Services.AddSingleton(new TableServiceClient(storageConnectionString));

// 3. Register your Email Service
builder.Services.AddScoped<IEmailSender, GmailSmtpService>();

builder.Build().Run();
