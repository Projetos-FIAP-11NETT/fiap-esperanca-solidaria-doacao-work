using Amazon.SQS;
using Amazon.SQS.Model;

using EsperancaSolidaria.Doacao.Application.Interfaces;

using Microsoft.Extensions.Options;

namespace EsperancaSolidaria.Doacao.Infrastructure.Messaging;

/// <summary>
/// Unico ponto do repositorio que conhece o SQS. Trocar de broker significa escrever outra
/// implementacao de <see cref="IPaymentQueue"/> — nada em Domain ou Application muda.
/// </summary>
internal sealed class SqsPaymentQueue(IAmazonSQS sqs, IOptions<SqsOptions> options) : IPaymentQueue
{
    private readonly SqsOptions options = options.Value;

    public async Task<IReadOnlyList<QueuedMessage>> ReceiveAsync(CancellationToken cancellationToken)
    {
        var response = await sqs.ReceiveMessageAsync(
            new ReceiveMessageRequest
            {
                QueueUrl = options.QueueUrl,
                MaxNumberOfMessages = options.MaxNumberOfMessages,
                WaitTimeSeconds = options.WaitTimeSeconds,
                VisibilityTimeout = options.VisibilityTimeoutSeconds,
            },
            cancellationToken);

        var messages = response.Messages;
        if (messages is null || messages.Count == 0)
        {
            return [];
        }

        return [.. messages.Select(message =>
            new QueuedMessage(message.MessageId, message.ReceiptHandle, message.Body))];
    }

    public Task AcknowledgeAsync(QueuedMessage message, CancellationToken cancellationToken) =>
        sqs.DeleteMessageAsync(options.QueueUrl, message.ReceiptHandle, cancellationToken);
}
