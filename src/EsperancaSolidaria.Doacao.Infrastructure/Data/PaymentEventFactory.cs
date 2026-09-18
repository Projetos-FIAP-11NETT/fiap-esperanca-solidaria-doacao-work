using EsperancaSolidaria.Doacao.Domain.Entities;
using EsperancaSolidaria.Doacao.Domain.Enums;
using EsperancaSolidaria.Doacao.Infrastructure.Data.Configurations;

namespace EsperancaSolidaria.Doacao.Infrastructure.Data;

/// <summary>
/// Monta o registro de auditoria com o que so a infraestrutura sabe: o relogio, a geracao do
/// identificador e o limite da coluna.
/// </summary>
internal static class PaymentEventFactory
{
    public static PaymentEvent Create(Guid donationId, PaymentEventType paymentEventType, string observation) =>
        new(
            Guid.CreateVersion7(),
            donationId,
            paymentEventType,
            Truncate(observation),
            DateTime.UtcNow);

    /// <summary>
    /// Mensagem de excecao longa nao pode derrubar o registro da falha — aqui a observacao
    /// e cortada em vez de estourar o limite da coluna.
    /// </summary>
    private static string Truncate(string observation) =>
        observation.Length <= PaymentEventConfiguration.ObservationMaxLength
            ? observation
            : observation[..PaymentEventConfiguration.ObservationMaxLength];
}
