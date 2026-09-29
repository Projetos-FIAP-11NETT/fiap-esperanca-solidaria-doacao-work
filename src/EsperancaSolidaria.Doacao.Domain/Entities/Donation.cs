using EsperancaSolidaria.Doacao.Domain.Enums;

namespace EsperancaSolidaria.Doacao.Domain.Entities;

/// <summary>
/// Doacao criada pela API quando o doador manifesta interesse. A tabela e da API:
/// o Worker so escreve em <see cref="DonationStatus"/>.
/// </summary>
/// <remarks>
/// Pelo mesmo criterio de <see cref="Campaign"/>, so as colunas que o Worker usa estao
/// mapeadas: DonorId e CreateAt existem na tabela mas nao participam do processamento.
/// </remarks>
public sealed class Donation
{
    public Guid DonationId { get; private set; }

    public Guid CampaignId { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    public DonationStatus DonationStatus { get; private set; }

    public Donation(
        Guid donationId,
        Guid campaignId,
        decimal amount,
        PaymentMethod paymentMethod,
        DonationStatus status)
    {
        DonationId = donationId;
        CampaignId = campaignId;
        Amount = amount;
        PaymentMethod = paymentMethod;
        DonationStatus = status;
    }

    private Donation()
    {
    }
}
