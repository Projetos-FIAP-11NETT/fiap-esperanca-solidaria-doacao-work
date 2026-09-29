namespace EsperancaSolidaria.Doacao.Domain.Enums;

/// <summary>
/// Forma de pagamento escolhida pelo doador. Cada uma tem uma chance propria de
/// aprovacao na simulacao do Worker, configurada em <c>Payments:ApprovalRate</c>.
/// </summary>
public enum PaymentMethod
{
    CreditCard = 1,
    DebitCard = 2,
    Pix = 3,
}
