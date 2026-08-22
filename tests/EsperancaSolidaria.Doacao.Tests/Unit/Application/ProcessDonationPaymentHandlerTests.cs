using EsperancaSolidaria.Doacao.Application.Commands;
using EsperancaSolidaria.Doacao.Domain.Entities;
using EsperancaSolidaria.Doacao.Domain.Enums;

using Microsoft.Extensions.Logging.Abstractions;

namespace EsperancaSolidaria.Doacao.Tests.Unit.Application;

public sealed class ProcessDonationPaymentHandlerTests
{
    private static readonly Guid DonationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CampaignId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Pagamento_aprovado_credita_a_campanha_e_registra_a_trilha()
    {
        var scenario = new Scenario(approves: true);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.Approved, outcome);
        Assert.Equal(DonationStatus.Approved, scenario.Donations.Current!.Status);
        Assert.Equal(150m, scenario.Campaigns.Credited);
        Assert.True(scenario.UnitOfWork.Committed);

        // Pending -> PaymentProcessing -> Approved, cada passo com o seu PaymentEvent.
        Assert.Equal(
            [DonationStatus.PaymentProcessing, DonationStatus.Approved],
            scenario.Donations.Transitions);
        Assert.Equal(2, scenario.PaymentEvents.Recorded.Count);
        Assert.All(scenario.PaymentEvents.Recorded, recorded =>
            Assert.Equal(PaymentEventType.Info, recorded.Type));
    }

    [Fact]
    public async Task Pagamento_reprovado_no_sorteio_nao_credita_a_campanha()
    {
        var scenario = new Scenario(approves: false);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.Rejected, outcome);
        Assert.Equal(DonationStatus.Rejected, scenario.Donations.Current!.Status);
        Assert.Equal(0m, scenario.Campaigns.Credited);
        Assert.True(scenario.UnitOfWork.Committed);
    }

    [Theory]
    [InlineData(CampaignStatus.Cancelled)]
    [InlineData(CampaignStatus.Completed)]
    public async Task Campanha_encerrada_rejeita_sem_sortear(CampaignStatus campaignStatus)
    {
        var scenario = new Scenario(approves: true, campaignStatus: campaignStatus);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.Rejected, outcome);
        Assert.Equal(DonationStatus.Rejected, scenario.Donations.Current!.Status);
        Assert.Equal(0m, scenario.Campaigns.Credited);

        // O sorteio nem chega a acontecer: a regra de negocio decide antes.
        Assert.Equal(0, scenario.Simulator.Calls);
        Assert.Contains(
            scenario.PaymentEvents.Recorded,
            recorded => recorded.Observation.Contains(campaignStatus.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Campanha_inexistente_rejeita_a_doacao()
    {
        var scenario = new Scenario(approves: true, campaign: false);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.Rejected, outcome);
        Assert.Equal(DonationStatus.Rejected, scenario.Donations.Current!.Status);
        Assert.Equal(0m, scenario.Campaigns.Credited);
    }

    [Fact]
    public async Task Mensagem_repetida_registra_warning_e_nao_credita_de_novo()
    {
        // Doacao ja aprovada por uma execucao anterior: e a reentrega do SQS chegando.
        var scenario = new Scenario(approves: true, donationStatus: DonationStatus.Approved);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.AlreadyProcessed, outcome);
        Assert.Equal(DonationStatus.Approved, scenario.Donations.Current!.Status);
        Assert.Empty(scenario.Donations.Transitions);
        Assert.Equal(0m, scenario.Campaigns.Credited);
        Assert.Equal(0, scenario.Simulator.Calls);

        var recorded = Assert.Single(scenario.PaymentEvents.Recorded);
        Assert.Equal(PaymentEventType.Warning, recorded.Type);

        // A mensagem e confirmada: reentregar de novo nao mudaria nada.
        Assert.True(scenario.UnitOfWork.Committed);
    }

    [Fact]
    public async Task Doacao_em_processamento_por_outra_replica_nao_e_reprocessada()
    {
        var scenario = new Scenario(approves: true, donationStatus: DonationStatus.PaymentProcessing);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.AlreadyProcessed, outcome);
        Assert.Equal(0m, scenario.Campaigns.Credited);
        Assert.Equal(PaymentEventType.Warning, Assert.Single(scenario.PaymentEvents.Recorded).Type);
    }

    [Fact]
    public async Task Doacao_inexistente_e_descartada_sem_registro_de_evento()
    {
        var scenario = new Scenario(approves: true, donation: false);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.DonationNotFound, outcome);

        // PaymentEvent referencia DonationId: sem doacao nao ha o que registrar.
        Assert.Empty(scenario.PaymentEvents.Recorded);
    }

    [Fact]
    public async Task Falha_de_infraestrutura_desfaz_tudo_grava_critical_e_propaga()
    {
        var failure = new InvalidOperationException("Postgres fora do ar");
        var scenario = new Scenario(approves: true, failOnStart: failure);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(scenario.HandleAsync);

        Assert.Same(failure, thrown);
        Assert.True(scenario.UnitOfWork.RolledBack);
        Assert.False(scenario.UnitOfWork.Committed);

        // A doacao continua Pending: falha de infraestrutura nunca vira Rejected.
        Assert.Equal(DonationStatus.Pending, scenario.Donations.Current!.Status);
        Assert.Equal(0m, scenario.Campaigns.Credited);

        // O Critical e gravado por fora da transacao desfeita — nunca falhar sem logar.
        Assert.Equal(failure.Message, Assert.Single(scenario.PaymentEventLogger.Critical));
    }

    /// <summary>Monta o caso de uso com todas as ports dubladas.</summary>
    private sealed class Scenario
    {
        public Scenario(
            bool approves,
            DonationStatus donationStatus = DonationStatus.Pending,
            CampaignStatus campaignStatus = CampaignStatus.Active,
            Exception? failOnStart = null,
            bool donation = true,
            bool campaign = true)
        {
            Donations = new FakeDonationRepository(
                donation
                    ? new Donation(DonationId, CampaignId, 150m, PaymentMethod.Pix, donationStatus)
                    : null)
            {
                FailOnStart = failOnStart,
            };

            Campaigns = new FakeCampaignRepository(
                campaign ? new Campaign(CampaignId, 1_000m, campaignStatus) : null);

            Simulator = new StubPaymentSimulator(approves);

            Handler = new ProcessDonationPaymentHandler(
                Donations,
                Campaigns,
                PaymentEvents,
                PaymentEventLogger,
                Simulator,
                UnitOfWork,
                NullLogger<ProcessDonationPaymentHandler>.Instance);
        }

        public FakeDonationRepository Donations { get; }

        public FakeCampaignRepository Campaigns { get; }

        public FakePaymentEventRepository PaymentEvents { get; } = new();

        public FakePaymentEventLogger PaymentEventLogger { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public StubPaymentSimulator Simulator { get; }

        private ProcessDonationPaymentHandler Handler { get; }

        public Task<PaymentProcessingOutcome> HandleAsync() =>
            Handler.HandleAsync(new DonationReceivedEvent(DonationId, Guid.NewGuid()), CancellationToken.None);
    }
}
