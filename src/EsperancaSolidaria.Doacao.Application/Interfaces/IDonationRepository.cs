using EsperancaSolidaria.Doacao.Domain.Entities;
using EsperancaSolidaria.Doacao.Domain.Enums;

namespace EsperancaSolidaria.Doacao.Application.Interfaces;

public interface IDonationRepository
{
    Task<Donation?> GetAsync(Guid donationId, CancellationToken cancellationToken);

    /// <summary>
    /// Porta de idempotencia: promove a doacao de <see cref="DonationStatus.Pending"/> para
    /// <see cref="DonationStatus.PaymentProcessing"/> em um UPDATE condicional.
    /// </summary>
    /// <returns>
    /// 1 quando esta execucao ganhou a doacao; 0 quando a doacao nao existe ou ja saiu de
    /// <c>Pending</c> — ou seja, quando a mensagem e uma reentrega.
    /// </returns>
    Task<int> TryStartProcessingAsync(Guid donationId, CancellationToken cancellationToken);

    /// <summary>
    /// Grava o desfecho, exigindo que a doacao ainda esteja em
    /// <see cref="DonationStatus.PaymentProcessing"/>.
    /// </summary>
    Task SetOutcomeAsync(Guid donationId, DonationStatus outcome, CancellationToken cancellationToken);
}
