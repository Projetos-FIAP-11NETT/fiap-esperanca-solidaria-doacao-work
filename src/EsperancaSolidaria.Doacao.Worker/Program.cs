using EsperancaSolidaria.Doacao.Infrastructure;
using EsperancaSolidaria.Doacao.Worker;
using EsperancaSolidaria.Doacao.Worker.Logging;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Console;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(console => console.FormatterName = DoacaoWorkerConsoleFormatter.FormatterName);
builder.Logging.AddConsoleFormatter<DoacaoWorkerConsoleFormatter, ConsoleFormatterOptions>(formatter =>
{
    formatter.IncludeScopes = true;
    formatter.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
    formatter.UseUtcTimestamp = true;
});

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DonationPaymentWorker>();

var app = builder.Build();

app.MapObservabilityEndpoints();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

app.Run();
