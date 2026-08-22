namespace EsperancaSolidaria.Doacao.Application.Interfaces;

/// <summary>
/// Registra um <c>PaymentEvent</c> <c>Critical</c> fora da transacao do processamento, em
/// conexao propria.
/// </summary>
/// <remarks>
/// Existe porque a exigencia e "nunca falhar sem logar": se o Critical fosse gravado pela
/// mesma transacao que acabou de ser desfeita, o rollback levaria o registro junto e a falha
/// sumiria do historico.
/// </remarks>
public interface IPaymentEventLogger
{
    Task RecordCriticalAsync(Guid donationId, string observation, CancellationToken cancellationToken);
}
