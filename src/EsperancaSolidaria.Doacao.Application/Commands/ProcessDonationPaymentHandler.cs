using System.Globalization;

using EsperancaSolidaria.Doacao.Application.Interfaces;
using EsperancaSolidaria.Doacao.Domain.Entities;
using EsperancaSolidaria.Doacao.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace EsperancaSolidaria.Doacao.Application.Commands;

/// <summary>
/// Caso de uso central do Worker: recebe um <see cref="DonationReceivedEvent"/> e leva a
/// doacao de <c>Pending</c> ate <c>Approved</c> ou <c>Rejected</c>, creditando a campanha
/// quando aprovada e registrando cada etapa em <c>PaymentEvent</c>.
/// </summary>
/// <remarks>
/// <para>
/// Tudo acontece em <b>uma transacao so</b>. A primeira escrita e um UPDATE condicional
/// (<c>WHERE Status = Pending</c>) que funciona como porta de idempotencia: o SQS entrega ao
/// menos uma vez e o Worker roda em varias replicas, entao duas execucoes podem chegar juntas
/// na mesma doacao. A que perder o UPDATE enxerga zero linhas afetadas, registra um
/// <c>Warning</c> e sai sem creditar.
/// </para>
/// <para>
/// Excecao nao tratada desfaz a transacao inteira, grava um <c>Critical</c> por fora dela e
/// sobe — a mensagem nao e confirmada e o SQS reentrega. Uma doacao nunca vira
/// <c>Rejected</c> por falha de infraestrutura; <c>Rejected</c> e sempre uma decisao de
/// negocio (campanha fora do ar ou sorteio).
/// </para>
/// <para>
/// Cada etapa vai para o log da aplicacao <b>e</b> para <c>PaymentEvent</c>. Os dois tem
/// publicos diferentes: o log serve ao diagnostico enquanto o pod roda; a tabela e a trilha
/// de auditoria que sobrevive a ele.
/// </para>
/// </remarks>
public sealed class ProcessDonationPaymentHandler(
    IDonationRepository donations,
    ICampaignRepository campaigns,
    IPaymentEventRepository paymentEvents,
    IPaymentEventLogger criticalLogger,
    IPaymentSimulator simulator,
    IUnitOfWork unitOfWork,
    ILogger<ProcessDonationPaymentHandler> logger)
{
    public async Task<PaymentProcessingOutcome> HandleAsync(
        DonationReceivedEvent message,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Abrindo a transacao da doacao {DonationId}.", message.DonationId);
        await unitOfWork.BeginAsync(cancellationToken);

        try
        {
            var outcome = await ProcessAsync(message, cancellationToken);

            await unitOfWork.CommitAsync(cancellationToken);
            logger.LogInformation(
                "Transacao da doacao {DonationId} confirmada. Desfecho: {Outcome}.",
                message.DonationId,
                outcome);

            return outcome;
        }
        catch (Exception exception)
        {
            await RollbackAndRecordCriticalAsync(message.DonationId, exception);
            throw;
        }
    }

    private async Task<PaymentProcessingOutcome> ProcessAsync(
        DonationReceivedEvent message,
        CancellationToken cancellationToken)
    {
        var donationId = message.DonationId;

        // Porta de idempotencia. Quem nao afeta nenhuma linha chegou atrasado.
        if (await donations.TryStartProcessingAsync(donationId, cancellationToken) == 0)
        {
            logger.LogInformation(
                "Doacao {DonationId} nao estava em Pending: zero linhas na porta de idempotencia.",
                donationId);
            return await HandleDuplicateAsync(donationId, cancellationToken);
        }

        logger.LogInformation("Doacao {DonationId} promovida de Pending para PaymentProcessing.", donationId);

        var donation = await donations.GetAsync(donationId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Doacao {donationId} sumiu do banco entre a promocao para PaymentProcessing e a leitura.");

        logger.LogInformation(
            "Doacao {DonationId} carregada: {Amount} via {PaymentMethod} para a campanha {CampaignId}.",
            donationId,
            Format(donation.Amount),
            donation.PaymentMethod,
            donation.CampaignId);

        await paymentEvents.RecordAsync(
            donationId,
            PaymentEventType.Info,
            $"Pagamento em processamento. Forma de pagamento: {donation.PaymentMethod}.",
            cancellationToken);

        var campaign = await campaigns.GetAsync(donation.CampaignId, cancellationToken);
        if (campaign is null || campaign.Status != CampaignStatus.Active)
        {
            logger.LogInformation(
                "Campanha {CampaignId} nao pode receber credito: {CampaignStatus}.",
                donation.CampaignId,
                campaign is null ? "inexistente" : campaign.Status.ToString());

            return await RejectAsync(
                donation,
                DescribeIneligibleCampaign(donation.CampaignId, campaign),
                cancellationToken);
        }

        logger.LogInformation("Campanha {CampaignId} esta Active; seguindo para a simulacao.", donation.CampaignId);

        var approved = simulator.Approves(donation.PaymentMethod);
        logger.LogInformation(
            "Simulacao de {PaymentMethod} para a doacao {DonationId}: {SimulationResult}.",
            donation.PaymentMethod,
            donationId,
            approved ? "aprovado" : "rejeitado");

        if (!approved)
        {
            return await RejectAsync(
                donation,
                $"Pagamento rejeitado na simulacao de {donation.PaymentMethod}.",
                cancellationToken);
        }

        return await ApproveAsync(donation, cancellationToken);
    }

    private async Task<PaymentProcessingOutcome> HandleDuplicateAsync(
        Guid donationId,
        CancellationToken cancellationToken)
    {
        var donation = await donations.GetAsync(donationId, cancellationToken);

        if (donation is null)
        {
            // Sem doacao nao ha como gravar PaymentEvent — a tabela referencia DonationId.
            logger.LogWarning(
                "Mensagem descartada: a doacao {DonationId} nao existe no banco.",
                donationId);
            return PaymentProcessingOutcome.DonationNotFound;
        }

        await paymentEvents.RecordAsync(
            donationId,
            PaymentEventType.Warning,
            $"Mensagem repetida ignorada: a doacao ja esta em {donation.Status}.",
            cancellationToken);

        logger.LogWarning(
            "Doacao {DonationId} ja estava em {Status}; mensagem confirmada sem reprocessar.",
            donationId,
            donation.Status);

        return PaymentProcessingOutcome.AlreadyProcessed;
    }

    private async Task<PaymentProcessingOutcome> ApproveAsync(
        Donation donation,
        CancellationToken cancellationToken)
    {
        await donations.SetOutcomeAsync(donation.DonationId, DonationStatus.Approved, cancellationToken);
        logger.LogInformation("Doacao {DonationId} marcada como Approved.", donation.DonationId);

        var credited = await campaigns.AddToTotalRaisedAsync(
            donation.CampaignId,
            donation.Amount,
            cancellationToken);

        if (credited == 0)
        {
            // A campanha saiu de Active entre a leitura e o credito. Desfaz tudo e deixa a
            // mensagem reentregar: no proximo giro ela sera rejeitada pelo caminho normal.
            throw new InvalidOperationException(
                $"Campanha {donation.CampaignId} deixou de estar ativa durante o processamento da doacao {donation.DonationId}.");
        }

        var amount = Format(donation.Amount);

        await paymentEvents.RecordAsync(
            donation.DonationId,
            PaymentEventType.Info,
            $"Pagamento aprovado via {donation.PaymentMethod}. Valor de {amount} creditado na campanha {donation.CampaignId}.",
            cancellationToken);

        logger.LogInformation(
            "Doacao {DonationId} aprovada: {Amount} creditados na campanha {CampaignId}.",
            donation.DonationId,
            amount,
            donation.CampaignId);

        return PaymentProcessingOutcome.Approved;
    }

    private async Task<PaymentProcessingOutcome> RejectAsync(
        Donation donation,
        string observation,
        CancellationToken cancellationToken)
    {
        await donations.SetOutcomeAsync(donation.DonationId, DonationStatus.Rejected, cancellationToken);

        await paymentEvents.RecordAsync(
            donation.DonationId,
            PaymentEventType.Info,
            observation,
            cancellationToken);

        logger.LogInformation(
            "Doacao {DonationId} marcada como Rejected. {Observation}",
            donation.DonationId,
            observation);

        return PaymentProcessingOutcome.Rejected;
    }

    private static string DescribeIneligibleCampaign(Guid campaignId, Campaign? campaign) =>
        campaign is null
            ? $"Pagamento rejeitado pois a campanha {campaignId} nao foi encontrada."
            : $"Pagamento rejeitado pois a campanha esta {campaign.Status}.";

    private static string Format(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    private async Task RollbackAndRecordCriticalAsync(Guid donationId, Exception exception)
    {
        logger.LogError(exception, "Falha ao processar o pagamento da doacao {DonationId}.", donationId);

        // O token original pode ja estar cancelado; desfazer e auditar nao sao opcionais.
        try
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            logger.LogWarning("Transacao da doacao {DonationId} desfeita.", donationId);
        }
        catch (Exception rollbackException)
        {
            logger.LogError(rollbackException, "Falha ao desfazer a transacao da doacao {DonationId}.", donationId);
        }

        try
        {
            await criticalLogger.RecordCriticalAsync(donationId, exception.Message, CancellationToken.None);
            logger.LogInformation(
                "PaymentEvent Critical da doacao {DonationId} gravado em conexao propria.",
                donationId);
        }
        catch (Exception loggingException)
        {
            // Ultimo recurso: se nem a conexao propria escreve, o log da aplicacao e o registro.
            logger.LogError(
                loggingException,
                "Falha ao registrar o PaymentEvent Critical da doacao {DonationId}.",
                donationId);
        }
    }
}
