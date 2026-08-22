using EsperancaSolidaria.Doacao.Domain.Entities;

namespace EsperancaSolidaria.Doacao.Application.Interfaces;

public interface ICampaignRepository
{
    Task<Campaign?> GetAsync(Guid campaignId, CancellationToken cancellationToken);

    /// <summary>
    /// Credita o valor da doacao somando no proprio banco
    /// (<c>TotalRaised = TotalRaised + @amount</c>), nunca com read-modify-write — duas
    /// replicas creditando a mesma campanha ao mesmo tempo perderiam uma das somas.
    /// A condicao de campanha ativa vai junto no UPDATE.
    /// </summary>
    /// <returns>1 quando creditou; 0 quando a campanha deixou de estar ativa nesse intervalo.</returns>
    Task<int> AddToTotalRaisedAsync(Guid campaignId, decimal amount, CancellationToken cancellationToken);
}
