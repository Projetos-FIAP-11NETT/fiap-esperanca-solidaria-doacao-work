using EsperancaSolidaria.Doacao.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsperancaSolidaria.Doacao.Infrastructure.Data.Configurations;

/// <summary>
/// Tabela <c>Donation</c>, criada e mantida pela API. DonorId e CreateAt existem na tabela mas
/// ficam de fora do mapeamento pelo mesmo criterio da <c>Campaign</c>: o Worker nao os usa.
/// Se o schema real usar outra convencao de nomes (snake_case, por exemplo), o ajuste e
/// inteiro aqui — dominio e caso de uso nao mudam.
/// </summary>
internal sealed class DonationConfiguration : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> builder)
    {
        builder.ToTable("Donation");

        builder.HasKey(donation => donation.DonationId);

        builder.Property(donation => donation.DonationId)
            .HasColumnName("DonationId")
            .ValueGeneratedNever();

        builder.Property(donation => donation.CampaignId)
            .HasColumnName("CampaignId");

        builder.Property(donation => donation.Amount)
            .HasColumnName("Amount")
            .HasPrecision(18, 2);

        // Os enums sao gravados pelos valores numericos do contrato (ver DonationStatus).
        builder.Property(donation => donation.PaymentMethod)
            .HasColumnName("PaymentMethod")
            .HasConversion<int>();

        builder.Property(donation => donation.Status)
            .HasColumnName("Status")
            .HasConversion<int>();
    }
}
