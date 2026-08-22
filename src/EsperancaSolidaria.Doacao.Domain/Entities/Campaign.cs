using EsperancaSolidaria.Doacao.Domain.Enums;

namespace EsperancaSolidaria.Doacao.Domain.Entities;

/// <summary>
/// Campanha de arrecadacao. Tambem e uma tabela da API — o Worker so soma em
/// <see cref="TotalRaised"/>, e apenas quando a campanha esta <see cref="CampaignStatus.Active"/>.
/// </summary>
/// <remarks>
/// Somente as colunas que o Worker usa estao mapeadas. Title, Description, StartDate, EndDate,
/// Image e FinancialGoal existem na tabela mas ficam de fora: coluna nao mapeada nao entra no
/// SELECT, nao pode ser escrita por engano e nao quebra o Worker se a API renomea-la.
/// </remarks>
public sealed class Campaign
{
    public Guid CampaignId { get; private set; }

    public decimal TotalRaised { get; private set; }

    public CampaignStatus Status { get; private set; }

    public Campaign(Guid campaignId, decimal totalRaised, CampaignStatus status)
    {
        CampaignId = campaignId;
        TotalRaised = totalRaised;
        Status = status;
    }

    // Construtor usado pelo EF Core na materializacao.
    private Campaign()
    {
    }
}
