using EsperancaSolidaria.Doacao.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsperancaSolidaria.Doacao.Infrastructure.Data.Configurations;

/// <summary>
/// Tabela <c>Campaign</c>, da API. Title, Description, StartDate, EndDate, Image e
/// FinancialGoal existem na tabela mas ficam de fora do mapeamento: o Worker nao os le, e
/// coluna nao mapeada nao entra no SELECT nem pode ser escrita por engano.
/// </summary>
internal sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaign");

        builder.HasKey(campaign => campaign.CampaignId);

        builder.Property(campaign => campaign.CampaignId)
            .HasColumnName("CampaignId")
            .ValueGeneratedNever();

        // Unica coluna que o Worker escreve, e sempre por soma no proprio banco.
        builder.Property(campaign => campaign.TotalRaised)
            .HasColumnName("TotalRaised")
            .HasPrecision(18, 2);

        builder.Property(campaign => campaign.Status)
            .HasColumnName("Status")
            .HasConversion<int>();
    }
}
