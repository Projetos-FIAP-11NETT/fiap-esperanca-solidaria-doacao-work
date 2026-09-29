using EsperancaSolidaria.Doacao.Domain.Enums;

namespace EsperancaSolidaria.Doacao.Infrastructure.Payments;

/// <summary>
/// Secao <c>Payments</c>: a chance de aprovacao de cada forma de pagamento, entre 0 e 1.
/// </summary>
/// <remarks>
/// Fica em configuracao, e nao no codigo, para que as taxas possam ser mexidas durante a
/// apresentacao sem recompilar — inclusive por variavel de ambiente
/// (<c>Payments__ApprovalRate__Pix</c>).
/// </remarks>
public sealed class PaymentSimulationOptions
{
    public const string SectionName = "Payments";

    public Dictionary<PaymentMethod, double> ApprovalRate { get; init; } = [];

    /// <summary>
    /// Percorre todas as formas de pagamento e reune os problemas de uma vez, para que o
    /// arranque mostre a lista inteira em vez de derrubar o processo no primeiro erro.
    /// </summary>
    internal List<string> Validate()
    {
        var errors = new List<string>();

        foreach (var method in Enum.GetValues<PaymentMethod>())
        {
            if (!ApprovalRate.TryGetValue(method, out var rate))
            {
                errors.Add($"Payments:ApprovalRate:{method} nao foi configurada.");
            }
            else if (rate is < 0 or > 1)
            {
                errors.Add($"Payments:ApprovalRate:{method} precisa estar entre 0 e 1.");
            }
        }

        return errors;
    }
}
