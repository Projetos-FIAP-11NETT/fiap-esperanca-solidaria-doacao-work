using EsperancaSolidaria.Doacao.Domain.Enums;

namespace EsperancaSolidaria.Doacao.Application.Interfaces;

/// <summary>
/// Escreve em <c>PaymentEvent</c> dentro da transacao em curso — o registro tem o mesmo
/// destino do processamento: commita junto ou desaparece junto.
/// Para o caso em que a transacao morre, veja <see cref="IPaymentEventLogger"/>.
/// </summary>
public interface IPaymentEventRepository
{
    Task RecordAsync(
        Guid donationId,
        PaymentEventType paymentEventType,
        string observation,
        CancellationToken cancellationToken);
}
