namespace EsperancaSolidaria.Doacao.Application.Commands;

/// <summary>
/// Desfecho do processamento de uma mensagem. Todos os valores aqui sao conclusivos: a
/// mensagem pode ser confirmada. Falha de infraestrutura nao aparece nesta lista — ela sobe
/// como excecao, justamente para que a mensagem <em>nao</em> seja confirmada.
/// </summary>
public enum PaymentProcessingOutcome
{
    /// <summary>Pagamento aprovado no sorteio e valor creditado na campanha.</summary>
    Approved,

    /// <summary>Rejeitado pelo sorteio ou porque a campanha nao estava ativa.</summary>
    Rejected,

    /// <summary>Reentrega: a doacao ja havia saido de <c>Pending</c>.</summary>
    AlreadyProcessed,

    /// <summary>O <c>DonationId</c> da mensagem nao existe no banco.</summary>
    DonationNotFound,
}
