namespace EsperancaSolidaria.Doacao.Application.Interfaces;

/// <summary>
/// Mensagem crua da fila. O corpo so e desserializado pelo host, que e quem decide confirmar
/// ou nao a mensagem.
/// </summary>
public sealed record QueuedMessage(string MessageId, string ReceiptHandle, string Body);

/// <summary>
/// Isola o broker do resto da aplicacao — nem o dominio nem o caso de uso sabem que existe SQS.
/// </summary>
public interface IPaymentQueue
{
    Task<IReadOnlyList<QueuedMessage>> ReceiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Confirma a mensagem, removendo-a da fila. Chamado somente depois do commit: falhar
    /// antes disso significa reentrega e, apos o <c>maxReceiveCount</c>, DLQ.
    /// </summary>
    Task AcknowledgeAsync(QueuedMessage message, CancellationToken cancellationToken);
}
