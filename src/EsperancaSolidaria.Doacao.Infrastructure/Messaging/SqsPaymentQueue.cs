using Amazon.SQS;
using Amazon.SQS.Model;

using EsperancaSolidaria.Doacao.Application.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EsperancaSolidaria.Doacao.Infrastructure.Messaging;

/// <summary>
/// Unico ponto do repositorio que conhece o SQS. Trocar de broker significa escrever outra
/// implementacao de <see cref="IPaymentQueue"/> — nada em Domain ou Application muda.
/// </summary>
/// <remarks>
/// Os logs daqui sao <c>Debug</c> de proposito: descrevem a conversa com o broker, util quando
/// o LocalStack nao responde, mas ruido no dia a dia. Ligue com
/// <c>Logging__LogLevel__EsperancaSolidaria=Debug</c>.
/// </remarks>
internal sealed class SqsPaymentQueue(
    IAmazonSQS sqs,
    IOptions<SqsOptions> options,
    ILogger<SqsPaymentQueue> logger) : IPaymentQueue
{
    private readonly SqsOptions options = options.Value;

    public async Task<IReadOnlyList<QueuedMessage>> ReceiveAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Consultando a fila {QueueUrl} (ate {MaxNumberOfMessages} mensagens, long polling de {WaitTimeSeconds}s).",
            options.QueueUrl,
            options.MaxNumberOfMessages,
            options.WaitTimeSeconds);

        var response = await sqs.ReceiveMessageAsync(
            new ReceiveMessageRequest
            {
                QueueUrl = options.QueueUrl,
                MaxNumberOfMessages = options.MaxNumberOfMessages,
                WaitTimeSeconds = options.WaitTimeSeconds,
                VisibilityTimeout = options.VisibilityTimeoutSeconds,
            },
            cancellationToken);

        // O AWS SDK v4 devolve null no lugar de colecao vazia.
        var messages = response.Messages;
        if (messages is null || messages.Count == 0)
        {
            return [];
        }

        logger.LogDebug(
            "A fila devolveu {MessageCount} mensagem(ns), invisiveis por {VisibilityTimeoutSeconds}s.",
            messages.Count,
            options.VisibilityTimeoutSeconds);

        return [.. messages.Select(message =>
            new QueuedMessage(message.MessageId, message.ReceiptHandle, message.Body))];
    }

    public async Task AcknowledgeAsync(QueuedMessage message, CancellationToken cancellationToken)
    {
        logger.LogDebug("Apagando a mensagem {MessageId} da fila {QueueUrl}.", message.MessageId, options.QueueUrl);
        await sqs.DeleteMessageAsync(options.QueueUrl, message.ReceiptHandle, cancellationToken);
    }
}
