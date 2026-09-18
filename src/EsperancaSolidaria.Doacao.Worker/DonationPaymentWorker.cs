using System.Diagnostics;
using System.Text.Json;

using EsperancaSolidaria.Doacao.Application.Interfaces;
using EsperancaSolidaria.Doacao.Application.Commands;

using Microsoft.Extensions.Options;

namespace EsperancaSolidaria.Doacao.Worker;

/// <summary>
/// Laco de consumo da fila. Recebe em lote por long polling, processa uma mensagem por vez e
/// so confirma o que chegou a um desfecho.
/// </summary>
/// <remarks>
/// Cada etapa e registrada: lote recebido, mensagem lida, escopo aberto, desfecho e
/// confirmacao. A etiqueta <c>[doacao-worker]</c> e o formato das linhas vem do
/// <see cref="Logging.DoacaoWorkerConsoleFormatter"/>.
/// </remarks>
public sealed class DonationPaymentWorker(
    IPaymentQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> workerOptions,
    ILogger<DonationPaymentWorker> logger) : BackgroundService
{
    /// <summary>Teto para uma mensagem em voo, para que um banco travado nao segure o pod.</summary>
    private readonly TimeSpan processingTimeout = TimeSpan.FromSeconds(
        workerOptions.Value.ProcessingTimeoutSeconds > 0
            ? workerOptions.Value.ProcessingTimeoutSeconds
            : throw new InvalidOperationException("Worker:ProcessingTimeoutSeconds precisa ser maior que zero."));

    /// <summary>Respiro depois de uma falha ao falar com a fila, para nao girar em vazio.</summary>
    private static readonly TimeSpan ReceiveFailureBackoff = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker de pagamentos iniciado. Aguardando mensagens da fila.");

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<QueuedMessage> messages;

            try
            {
                messages = await queue.ReceiveAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao ler a fila. Nova tentativa em {Backoff}.", ReceiveFailureBackoff);
                await DelayAsync(ReceiveFailureBackoff, stoppingToken);
                continue;
            }

            if (messages.Count == 0)
            {
                logger.LogDebug("Nenhuma mensagem na fila neste ciclo.");
                continue;
            }

            logger.LogInformation("Lote recebido da fila: {MessageCount} mensagem(ns).", messages.Count);

            foreach (var message in messages)
            {
                // Sem o stoppingToken: no SIGTERM as mensagens em voo terminam.
                await HandleAsync(message);
            }

            logger.LogInformation("Lote de {MessageCount} mensagem(ns) finalizado.", messages.Count);
        }

        logger.LogInformation("Worker de pagamentos encerrado.");
    }

    private async Task HandleAsync(QueuedMessage message)
    {
        logger.LogInformation(
            "Mensagem {MessageId} lida da fila. Corpo: {MessageBody}",
            message.MessageId,
            message.Body);

        var received = Deserialize(message);

        if (received is null)
        {
            await AcknowledgeAsync(message, CancellationToken.None);
            return;
        }

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["DonationId"] = received.DonationId,
            ["CorrelationId"] = received.CorrelationId,
        });

        logger.LogInformation(
            "Mensagem {MessageId} desserializada. Iniciando o processamento com teto de {Timeout}.",
            message.MessageId,
            processingTimeout);

        using var timeout = new CancellationTokenSource(processingTimeout);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var serviceScope = scopeFactory.CreateAsyncScope();
            var handler = serviceScope.ServiceProvider.GetRequiredService<ProcessDonationPaymentHandler>();

            var outcome = await handler.HandleAsync(received, timeout.Token);
            stopwatch.Stop();

            await AcknowledgeAsync(message, CancellationToken.None);

            logger.LogInformation(
                "Mensagem {MessageId} concluida como {Outcome} em {ElapsedMs} ms.",
                message.MessageId,
                outcome,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            logger.LogError(
                exception,
                "Mensagem {MessageId} falhou apos {ElapsedMs} ms; nao confirmada e devolvida a fila.",
                message.MessageId,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private DonationReceivedEvent? Deserialize(QueuedMessage message)
    {
        DonationReceivedEvent? received;

        try
        {
            received = JsonSerializer.Deserialize<DonationReceivedEvent>(message.Body, SerializerOptions);
        }
        catch (JsonException exception)
        {
            logger.LogError(
                exception,
                "Mensagem {MessageId} descartada: corpo nao e um DonationReceivedEvent valido.",
                message.MessageId);
            return null;
        }

        if (received is null || received.DonationId == Guid.Empty)
        {
            logger.LogError(
                "Mensagem {MessageId} descartada: DonationId ausente ou vazio.",
                message.MessageId);
            return null;
        }

        return received;
    }

    private async Task AcknowledgeAsync(QueuedMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await queue.AcknowledgeAsync(message, cancellationToken);
            logger.LogInformation("Mensagem {MessageId} confirmada e removida da fila.", message.MessageId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao confirmar a mensagem {MessageId}.", message.MessageId);
        }
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Encerrando: o laco sai na proxima verificacao.
        }
    }
}
