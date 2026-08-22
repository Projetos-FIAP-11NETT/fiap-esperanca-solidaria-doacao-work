using EsperancaSolidaria.Doacao.Application.Interfaces;
using EsperancaSolidaria.Doacao.Infrastructure.Data;
using EsperancaSolidaria.Doacao.Domain.Entities;
using EsperancaSolidaria.Doacao.Domain.Enums;

using Microsoft.EntityFrameworkCore;

namespace EsperancaSolidaria.Doacao.Infrastructure.Repositories;

/// <summary>
/// As duas escritas sao UPDATEs condicionais executados direto no banco
/// (<c>ExecuteUpdateAsync</c>), sem passar pelo change tracker: o proprio <c>WHERE</c> e o
/// mecanismo de concorrencia, e a contagem de linhas afetadas e a resposta.
/// </summary>
internal sealed class DonationRepository(EsperancaSolidariaDbContext context) : IDonationRepository
{
    public Task<Donation?> GetAsync(Guid donationId, CancellationToken cancellationToken) =>
        context.Donations
            .AsNoTracking()
            .FirstOrDefaultAsync(donation => donation.DonationId == donationId, cancellationToken);

    // UPDATE "Donation" SET "Status" = 2 WHERE "DonationId" = @id AND "Status" = 1
    public Task<int> TryStartProcessingAsync(Guid donationId, CancellationToken cancellationToken) =>
        context.Donations
            .Where(donation => donation.DonationId == donationId
                && donation.Status == DonationStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(donation => donation.Status, DonationStatus.PaymentProcessing),
                cancellationToken);

    // UPDATE "Donation" SET "Status" = @outcome WHERE "DonationId" = @id AND "Status" = 2
    public async Task SetOutcomeAsync(
        Guid donationId,
        DonationStatus outcome,
        CancellationToken cancellationToken) =>
        await context.Donations
            .Where(donation => donation.DonationId == donationId
                && donation.Status == DonationStatus.PaymentProcessing)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(donation => donation.Status, outcome),
                cancellationToken);
}
