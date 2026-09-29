using EsperancaSolidaria.Doacao.Application.Interfaces;
using EsperancaSolidaria.Doacao.Domain.Enums;

using Microsoft.EntityFrameworkCore;

namespace EsperancaSolidaria.Doacao.Infrastructure.Data;

/// <summary>
/// Grava o <c>Critical</c> em um <see cref="EsperancaSolidariaDbContext"/> proprio, criado na
/// hora pela factory.
/// </summary>
/// <remarks>
/// Usar o contexto do escopo nao serviria: quando este codigo roda, a transacao daquele
/// contexto acabou de ser desfeita, e o registro da falha sumiria junto com ela.
/// </remarks>
internal sealed class PaymentEventLogger(IDbContextFactory<EsperancaSolidariaDbContext> contextFactory)
    : IPaymentEventLogger
{
    public async Task RecordCriticalAsync(
        Guid donationId,
        string observation,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        context.PaymentEvents.Add(
            PaymentEventFactory.Create(donationId, PaymentEventType.Critical, observation));

        await context.SaveChangesAsync(cancellationToken);
    }
}
