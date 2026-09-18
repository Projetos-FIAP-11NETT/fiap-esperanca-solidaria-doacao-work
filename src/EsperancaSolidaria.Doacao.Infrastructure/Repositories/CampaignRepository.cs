using EsperancaSolidaria.Doacao.Application.Interfaces;
using EsperancaSolidaria.Doacao.Infrastructure.Data;
using EsperancaSolidaria.Doacao.Domain.Entities;
using EsperancaSolidaria.Doacao.Domain.Enums;

using Microsoft.EntityFrameworkCore;

namespace EsperancaSolidaria.Doacao.Infrastructure.Repositories;

internal sealed class CampaignRepository(EsperancaSolidariaDbContext context) : ICampaignRepository
{
    public Task<Campaign?> GetAsync(Guid campaignId, CancellationToken cancellationToken) =>
        context.Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(campaign => campaign.CampaignId == campaignId, cancellationToken);

    public Task<int> AddToTotalRaisedAsync(
        Guid campaignId,
        decimal amount,
        CancellationToken cancellationToken) =>
        context.Campaigns
            .Where(campaign => campaign.CampaignId == campaignId
                && campaign.Status == CampaignStatus.Active)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    campaign => campaign.TotalRaised,
                    campaign => campaign.TotalRaised + amount),
                cancellationToken);
}
