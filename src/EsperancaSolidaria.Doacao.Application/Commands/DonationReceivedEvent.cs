namespace EsperancaSolidaria.Doacao.Application.Commands;

/// <summary>
/// Contrato publicado pela API na fila. Carrega apenas identificadores: valor, campanha e
/// forma de pagamento sao lidos do banco, para que uma mensagem adulterada ou defasada nao
/// consiga creditar um valor diferente do que a doacao realmente tem.
/// </summary>
public sealed record DonationReceivedEvent(Guid DonationId, Guid CorrelationId);
