using EsperancaSolidaria.Doacao.Application.Interfaces;
using EsperancaSolidaria.Doacao.Domain.Entities;
using EsperancaSolidaria.Doacao.Domain.Enums;

namespace EsperancaSolidaria.Doacao.Tests.Unit.Application;

/// <summary>
/// Dublês em memoria para as ports. Sao eles que permitem exercitar o caso de uso inteiro —
/// idempotencia inclusive — sem Postgres e sem SQS no ar.
/// </summary>
internal sealed class FakeDonationRepository(Donation? donation) : IDonationRepository
{
    private Donation? donation = donation;

    /// <summary>Excecao devolvida na porta de idempotencia, para simular o banco fora do ar.</summary>
    public Exception? FailOnStart { get; init; }

    public List<DonationStatus> Transitions { get; } = [];

    public Donation? Current => donation;

    public Task<Donation?> GetAsync(Guid donationId, CancellationToken cancellationToken) =>
        Task.FromResult(donation?.DonationId == donationId ? donation : null);

    public Task<int> TryStartProcessingAsync(Guid donationId, CancellationToken cancellationToken)
    {
        if (FailOnStart is not null)
        {
            throw FailOnStart;
        }

        return Task.FromResult(TryMove(donationId, DonationStatus.Pending, DonationStatus.PaymentProcessing));
    }

    public Task SetOutcomeAsync(
        Guid donationId,
        DonationStatus outcome,
        CancellationToken cancellationToken)
    {
        TryMove(donationId, DonationStatus.PaymentProcessing, outcome);
        return Task.CompletedTask;
    }

    /// <summary>Reproduz o UPDATE condicional: so muda se o status atual for o esperado.</summary>
    private int TryMove(Guid donationId, DonationStatus expected, DonationStatus next)
    {
        if (donation is null || donation.DonationId != donationId || donation.Status != expected)
        {
            return 0;
        }

        donation = new Donation(
            donation.DonationId,
            donation.CampaignId,
            donation.Amount,
            donation.PaymentMethod,
            next);

        Transitions.Add(next);
        return 1;
    }
}

internal sealed class FakeCampaignRepository(Campaign? campaign) : ICampaignRepository
{
    public decimal Credited { get; private set; }

    public Task<Campaign?> GetAsync(Guid campaignId, CancellationToken cancellationToken) =>
        Task.FromResult(campaign?.CampaignId == campaignId ? campaign : null);

    public Task<int> AddToTotalRaisedAsync(
        Guid campaignId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (campaign is null || campaign.CampaignId != campaignId || campaign.Status != CampaignStatus.Active)
        {
            return Task.FromResult(0);
        }

        Credited += amount;
        return Task.FromResult(1);
    }
}

internal sealed class FakePaymentEventRepository : IPaymentEventRepository
{
    public List<(PaymentEventType Type, string Observation)> Recorded { get; } = [];

    public Task RecordAsync(
        Guid donationId,
        PaymentEventType paymentEventType,
        string observation,
        CancellationToken cancellationToken)
    {
        Recorded.Add((paymentEventType, observation));
        return Task.CompletedTask;
    }
}

internal sealed class FakePaymentEventLogger : IPaymentEventLogger
{
    public List<string> Critical { get; } = [];

    public Task RecordCriticalAsync(Guid donationId, string observation, CancellationToken cancellationToken)
    {
        Critical.Add(observation);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public bool Committed { get; private set; }

    public bool RolledBack { get; private set; }

    public Task BeginAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        Committed = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        RolledBack = true;
        return Task.CompletedTask;
    }
}

internal sealed class StubPaymentSimulator(bool approves) : IPaymentSimulator
{
    public int Calls { get; private set; }

    public bool Approves(PaymentMethod paymentMethod)
    {
        Calls++;
        return approves;
    }
}
