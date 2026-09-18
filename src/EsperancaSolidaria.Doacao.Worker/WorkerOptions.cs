namespace EsperancaSolidaria.Doacao.Worker;

/// <summary>
/// Secao <c>Worker</c>: ajustes do laco de consumo que nao pertencem ao broker.
/// </summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>
    /// Teto para uma mensagem em voo, para que um banco travado nao segure o pod.
    /// </summary>
    public int ProcessingTimeoutSeconds { get; set; } = 30;
}
