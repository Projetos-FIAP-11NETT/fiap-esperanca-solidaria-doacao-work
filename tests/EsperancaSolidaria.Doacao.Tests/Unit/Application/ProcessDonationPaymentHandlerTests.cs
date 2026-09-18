using EsperancaSolidaria.Doacao.Application.Commands;
using EsperancaSolidaria.Doacao.Application.Interfaces;
using EsperancaSolidaria.Doacao.Domain.Entities;
using EsperancaSolidaria.Doacao.Domain.Enums;

using Microsoft.Extensions.Logging.Abstractions;

namespace EsperancaSolidaria.Doacao.Tests.Unit.Application;

public sealed class ProcessDonationPaymentHandlerTests
{
    private static readonly Guid DonationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CampaignId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const decimal Amount = 150m;

    [Fact]
    public async Task Pagamento_aprovado_credita_a_campanha_e_registra_a_trilha()
    {
        var scenario = new Scenario(approves: true);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.Approved, outcome);

        scenario.Donations.Verify(
            d => d.SetOutcomeAsync(DonationId, DonationStatus.Approved, It.IsAny<CancellationToken>()),
            Times.Once);
        scenario.Campaigns.Verify(
            c => c.AddToTotalRaisedAsync(CampaignId, Amount, It.IsAny<CancellationToken>()),
            Times.Once);
        scenario.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

        scenario.PaymentEvents.Verify(
            p => p.RecordAsync(DonationId, PaymentEventType.Info, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        scenario.PaymentEvents.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Pagamento_reprovado_no_sorteio_nao_credita_a_campanha()
    {
        var scenario = new Scenario(approves: false);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.Rejected, outcome);

        scenario.Donations.Verify(
            d => d.SetOutcomeAsync(DonationId, DonationStatus.Rejected, It.IsAny<CancellationToken>()),
            Times.Once);
        scenario.Campaigns.Verify(
            c => c.AddToTotalRaisedAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
        scenario.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(CampaignStatus.Cancelled)]
    [InlineData(CampaignStatus.Completed)]
    public async Task Campanha_encerrada_rejeita_sem_sortear(CampaignStatus campaignStatus)
    {
        var scenario = new Scenario(approves: true, campaignStatus: campaignStatus);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.Rejected, outcome);

        scenario.Donations.Verify(
            d => d.SetOutcomeAsync(DonationId, DonationStatus.Rejected, It.IsAny<CancellationToken>()),
            Times.Once);
        scenario.Campaigns.Verify(
            c => c.AddToTotalRaisedAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
        scenario.Simulator.Verify(s => s.Approves(It.IsAny<PaymentMethod>()), Times.Never);

        scenario.PaymentEvents.Verify(
            p => p.RecordAsync(
                DonationId,
                PaymentEventType.Info,
                It.Is<string>(observation => observation.Contains(campaignStatus.ToString(), StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Campanha_inexistente_rejeita_a_doacao()
    {
        var scenario = new Scenario(approves: true, campaign: false);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.Rejected, outcome);

        scenario.Donations.Verify(
            d => d.SetOutcomeAsync(DonationId, DonationStatus.Rejected, It.IsAny<CancellationToken>()),
            Times.Once);
        scenario.Campaigns.Verify(
            c => c.AddToTotalRaisedAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Mensagem_repetida_registra_warning_e_nao_credita_de_novo()
    {
        var scenario = new Scenario(approves: true, donationStatus: DonationStatus.Approved);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.AlreadyProcessed, outcome);

        scenario.Donations.Verify(
            d => d.SetOutcomeAsync(It.IsAny<Guid>(), It.IsAny<DonationStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
        scenario.Campaigns.Verify(
            c => c.AddToTotalRaisedAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
        scenario.Simulator.Verify(s => s.Approves(It.IsAny<PaymentMethod>()), Times.Never);

        scenario.PaymentEvents.Verify(
            p => p.RecordAsync(DonationId, PaymentEventType.Warning, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        scenario.PaymentEvents.VerifyNoOtherCalls();

        scenario.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Doacao_em_processamento_por_outra_replica_nao_e_reprocessada()
    {
        var scenario = new Scenario(approves: true, donationStatus: DonationStatus.PaymentProcessing);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.AlreadyProcessed, outcome);

        scenario.Campaigns.Verify(
            c => c.AddToTotalRaisedAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
        scenario.PaymentEvents.Verify(
            p => p.RecordAsync(DonationId, PaymentEventType.Warning, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        scenario.PaymentEvents.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Doacao_inexistente_e_descartada_sem_registro_de_evento()
    {
        var scenario = new Scenario(approves: true, donation: false);

        var outcome = await scenario.HandleAsync();

        Assert.Equal(PaymentProcessingOutcome.DonationNotFound, outcome);

        scenario.PaymentEvents.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Falha_de_infraestrutura_desfaz_tudo_grava_critical_e_propaga()
    {
        var failure = new InvalidOperationException("Postgres fora do ar");
        var scenario = new Scenario(approves: true, failOnStart: failure);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(scenario.HandleAsync);

        Assert.Same(failure, thrown);

        scenario.UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        scenario.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);

        scenario.Donations.Verify(
            d => d.SetOutcomeAsync(It.IsAny<Guid>(), It.IsAny<DonationStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
        scenario.Campaigns.Verify(
            c => c.AddToTotalRaisedAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);

        scenario.PaymentEventLogger.Verify(
            l => l.RecordCriticalAsync(DonationId, failure.Message, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Monta o caso de uso com todas as ports substituidas por mocks, de modo que o
    /// processamento inteiro — idempotencia inclusive — seja exercitado sem Postgres e sem SQS.
    /// </summary>
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
            Donation? storedDonation = donation
                ? new Donation(DonationId, CampaignId, Amount, PaymentMethod.Pix, donationStatus)
                : null;

            Campaign? storedCampaign = campaign
                ? new Campaign(CampaignId, 1_000m, campaignStatus)
                : null;

            Donations
                .Setup(d => d.GetAsync(DonationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedDonation);

            var startProcessing = Donations.Setup(
                d => d.TryStartProcessingAsync(DonationId, It.IsAny<CancellationToken>()));

            if (failOnStart is not null)
            {
                startProcessing.ThrowsAsync(failOnStart);
            }
            else
            {
                startProcessing.ReturnsAsync(donationStatus == DonationStatus.Pending && donation ? 1 : 0);
            }

            Campaigns
                .Setup(c => c.GetAsync(CampaignId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedCampaign);

            Campaigns
                .Setup(c => c.AddToTotalRaisedAsync(CampaignId, It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(campaignStatus == CampaignStatus.Active && campaign ? 1 : 0);

            Simulator.Setup(s => s.Approves(It.IsAny<PaymentMethod>())).Returns(approves);

            Handler = new ProcessDonationPaymentHandler(
                Donations.Object,
                Campaigns.Object,
                PaymentEvents.Object,
                PaymentEventLogger.Object,
                Simulator.Object,
                UnitOfWork.Object,
                NullLogger<ProcessDonationPaymentHandler>.Instance);
        }

        public Mock<IDonationRepository> Donations { get; } = new();

        public Mock<ICampaignRepository> Campaigns { get; } = new();

        public Mock<IPaymentEventRepository> PaymentEvents { get; } = new();

        public Mock<IPaymentEventLogger> PaymentEventLogger { get; } = new();

        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public Mock<IPaymentSimulator> Simulator { get; } = new();

        private ProcessDonationPaymentHandler Handler { get; }

        public Task<PaymentProcessingOutcome> HandleAsync() =>
            Handler.HandleAsync(new DonationReceivedEvent(DonationId, Guid.NewGuid()), CancellationToken.None);
    }
}
