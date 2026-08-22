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

    internal IEnumerable<string> Validate()
    {
        foreach (var method in Enum.GetValues<PaymentMethod>())
        {
            if (!ApprovalRate.TryGetValue(method, out var rate))
            {
                yield return $"Payments:ApprovalRate:{method} nao foi configurada.";
            }
            else if (rate is < 0 or > 1)
            {
                yield return $"Payments:ApprovalRate:{method} precisa estar entre 0 e 1.";
            }
        }
    }
}
