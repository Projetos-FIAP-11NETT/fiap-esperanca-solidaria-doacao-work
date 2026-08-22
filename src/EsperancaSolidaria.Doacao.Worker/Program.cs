using EsperancaSolidaria.Doacao.Infrastructure;
using EsperancaSolidaria.Doacao.Worker;
using EsperancaSolidaria.Doacao.Worker.Logging;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Console;

var builder = WebApplication.CreateBuilder(args);

// Uma unica saida de log, com formato proprio: toda linha sai etiquetada com [doacao-worker],
// inclusive as do host e do ASP.NET Core. ClearProviders garante que nada escreva por fora.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(console => console.FormatterName = DoacaoWorkerConsoleFormatter.FormatterName);
builder.Logging.AddConsoleFormatter<DoacaoWorkerConsoleFormatter, ConsoleFormatterOptions>(formatter =>
{
    // Os escopos trazem DonationId e CorrelationId em cada linha do processamento.
    formatter.IncludeScopes = true;
    formatter.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
    // UTC: o pod pode rodar em qualquer fuso, e o log vai ser lido junto com o da API.
    formatter.UseUtcTimestamp = true;
});

// A configuracao chega por variavel de ambiente (Secret aplicado via kubectl pelo projeto
// de infraestrutura). O binding padrao do .NET ja le env vars com "__" no lugar de ":".
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DonationPaymentWorker>();

var app = builder.Build();

// Liveness: o processo esta de pe. Nao consulta dependencia nenhuma, senao um Postgres fora
// do ar faria o Kubernetes reiniciar o pod em vez de apenas tira-lo do trafego.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: as dependencias respondem (Postgres e SQS, registrados com a tag "ready").
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

app.Run();
