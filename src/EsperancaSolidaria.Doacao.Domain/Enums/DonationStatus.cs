namespace EsperancaSolidaria.Doacao.Domain.Enums;

/// <summary>
/// Ciclo de vida da doacao. Os valores numericos sao os gravados na coluna
/// <c>Donation.Status</c> e fazem parte do contrato com a API de campanhas.
/// </summary>
public enum DonationStatus
{
    Pending = 1,
    PaymentProcessing = 2,
    Approved = 3,
    Rejected = 4,
}
