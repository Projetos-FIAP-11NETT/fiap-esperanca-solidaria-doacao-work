using EsperancaSolidaria.Doacao.Domain.Enums;

namespace EsperancaSolidaria.Doacao.Domain.Entities;

/// <summary>
/// Registro de auditoria do processamento de um pagamento. Diferente de <c>Donation</c> e
/// <c>Campaign</c>, esta tabela e escrita exclusivamente pelo Worker: e o historico do que
/// aconteceu com cada doacao, em ordem.
/// </summary>
public sealed class PaymentEvent
{
    public Guid PaymentEventId { get; private set; }

    public Guid DonationId { get; private set; }

    public PaymentEventType PaymentEventType { get; private set; }

    public string Observation { get; private set; } = string.Empty;

    public DateTime CreateAt { get; private set; }

    public PaymentEvent(
        Guid paymentEventId,
        Guid donationId,
        PaymentEventType paymentEventType,
        string observation,
        DateTime createAt)
    {
        PaymentEventId = paymentEventId;
        DonationId = donationId;
        PaymentEventType = paymentEventType;
        Observation = observation;
        CreateAt = createAt;
    }

    private PaymentEvent()
    {
    }
}
