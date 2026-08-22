namespace EsperancaSolidaria.Doacao.Domain.Enums;

/// <summary>
/// Severidade do registro em <c>PaymentEvent</c>, a trilha de auditoria que o Worker
/// escreve a cada etapa do processamento.
/// </summary>
public enum PaymentEventType
{
    /// <summary>Curso normal: entrou em processamento, foi aprovado ou foi rejeitado.</summary>
    Info = 1,

    /// <summary>Excecao nao tratada — a doacao nao foi concluida e a mensagem sera reentregue.</summary>
    Critical = 2,

    /// <summary>Mensagem repetida: a doacao ja havia saido de <c>Pending</c>.</summary>
    Warning = 3,
}
