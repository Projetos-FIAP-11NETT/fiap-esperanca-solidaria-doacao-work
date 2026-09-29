using EsperancaSolidaria.Doacao.Application.Interfaces;
using EsperancaSolidaria.Doacao.Domain.Enums;

using Microsoft.Extensions.Options;

namespace EsperancaSolidaria.Doacao.Infrastructure.Payments;

/// <summary>
/// Substitui o adquirente que nao existe: sorteia o desfecho comparando um numero aleatorio
/// com a taxa de aprovacao configurada para a forma de pagamento.
/// </summary>
/// <remarks>
/// O sorteio acontece dentro da transacao, depois da porta de idempotencia. Numa reentrega o
/// <c>WHERE</c> do UPDATE nao casa e o resultado novo e descartado — uma doacao nunca muda de
/// ideia sobre ter sido aprovada.
/// </remarks>
internal sealed class RandomPaymentSimulator(IOptions<PaymentSimulationOptions> options) : IPaymentSimulator
{
    public bool Approves(PaymentMethod paymentMethod)
    {
        if (!options.Value.ApprovalRate.TryGetValue(paymentMethod, out var approvalRate))
        {
            throw new InvalidOperationException(
                $"Nao ha taxa de aprovacao configurada para {paymentMethod} em Payments:ApprovalRate.");
        }

        return Random.Shared.NextDouble() < approvalRate;
    }
}
