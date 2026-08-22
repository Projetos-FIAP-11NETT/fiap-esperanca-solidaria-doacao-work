using EsperancaSolidaria.Doacao.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EsperancaSolidaria.Doacao.Infrastructure.Data.Configurations;

/// <summary>
/// Tabela <c>PaymentEvent</c> — a unica que o Worker preenche. E a trilha de auditoria do
/// processamento, consultada por <c>DonationId</c>.
/// </summary>
internal sealed class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    /// <summary>Limite aplicado a <c>Observation</c> antes de gravar.</summary>
    public const int ObservationMaxLength = 1000;

    public void Configure(EntityTypeBuilder<PaymentEvent> builder)
    {
        builder.ToTable("PaymentEvent");

        builder.HasKey(paymentEvent => paymentEvent.PaymentEventId);

        builder.Property(paymentEvent => paymentEvent.PaymentEventId)
            .HasColumnName("PaymentEventId")
            .ValueGeneratedNever();

        builder.Property(paymentEvent => paymentEvent.DonationId)
            .HasColumnName("DonationId");

        builder.Property(paymentEvent => paymentEvent.PaymentEventType)
            .HasColumnName("PaymentEventType")
            .HasConversion<int>();

        builder.Property(paymentEvent => paymentEvent.Observation)
            .HasColumnName("Observation")
            .HasMaxLength(ObservationMaxLength);

        builder.Property(paymentEvent => paymentEvent.CreateAt)
            .HasColumnName("CreateAt");
    }
}
