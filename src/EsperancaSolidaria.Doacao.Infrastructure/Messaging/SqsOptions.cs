namespace EsperancaSolidaria.Doacao.Infrastructure.Messaging;

/// <summary>
/// Secao <c>Sqs</c>. A <see cref="QueueUrl"/> chega por variavel de ambiente
/// (<c>Sqs__QueueUrl</c>), do Secret aplicado pelo projeto de infraestrutura.
/// </summary>
public sealed class SqsOptions
{
    public const string SectionName = "Sqs";

    public string QueueUrl { get; set; } = string.Empty;

    /// <summary>Teto por chamada imposto pelo proprio SQS.</summary>
    public int MaxNumberOfMessages { get; set; } = 10;

    /// <summary>Long polling: espera ate haver mensagem em vez de girar em vazio.</summary>
    public int WaitTimeSeconds { get; set; } = 20;

    /// <summary>
    /// Tempo em que a mensagem fica invisivel para as outras replicas. Precisa cobrir o
    /// processamento inteiro, senao a mensagem reaparece enquanto ainda esta sendo tratada.
    /// </summary>
    public int VisibilityTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Reune todos os problemas da secao de uma vez, para que o arranque mostre a lista
    /// inteira em vez de derrubar o processo no primeiro erro.
    /// </summary>
    internal List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(QueueUrl))
        {
            errors.Add("Sqs:QueueUrl nao foi configurada.");
        }

        if (MaxNumberOfMessages is < 1 or > 10)
        {
            errors.Add("Sqs:MaxNumberOfMessages precisa estar entre 1 e 10.");
        }

        if (WaitTimeSeconds is < 0 or > 20)
        {
            errors.Add("Sqs:WaitTimeSeconds precisa estar entre 0 e 20.");
        }

        if (VisibilityTimeoutSeconds is < 0 or > 43200)
        {
            errors.Add("Sqs:VisibilityTimeoutSeconds precisa estar entre 0 e 43200.");
        }

        return errors;
    }
}
