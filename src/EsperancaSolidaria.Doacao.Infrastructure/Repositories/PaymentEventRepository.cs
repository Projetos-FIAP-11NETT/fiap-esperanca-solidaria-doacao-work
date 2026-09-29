using EsperancaSolidaria.Doacao.Application.Interfaces;
using EsperancaSolidaria.Doacao.Infrastructure.Data;
using EsperancaSolidaria.Doacao.Domain.Enums;

namespace EsperancaSolidaria.Doacao.Infrastructure.Repositories;

internal sealed class PaymentEventRepository(EsperancaSolidariaDbContext context) : IPaymentEventRepository
{
    public async Task RecordAsync(
        Guid donationId,
        PaymentEventType paymentEventType,
        string observation,
        CancellationToken cancellationToken)
    {
        context.PaymentEvents.Add(PaymentEventFactory.Create(donationId, paymentEventType, observation));
        await context.SaveChangesAsync(cancellationToken);
    }
}
