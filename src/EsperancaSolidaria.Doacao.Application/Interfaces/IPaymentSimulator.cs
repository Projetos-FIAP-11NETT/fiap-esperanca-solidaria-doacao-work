using EsperancaSolidaria.Doacao.Domain.Enums;

namespace EsperancaSolidaria.Doacao.Application.Interfaces;

/// <summary>
/// Sorteia o desfecho do pagamento a partir da taxa de aprovacao da forma escolhida.
/// Nao ha cobranca real: este e o ponto onde a simulacao acontece.
/// </summary>
public interface IPaymentSimulator
{
    bool Approves(PaymentMethod paymentMethod);
}
