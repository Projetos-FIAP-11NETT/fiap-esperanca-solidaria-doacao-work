namespace EsperancaSolidaria.Doacao.Domain.Enums;

/// <summary>
/// Situacao da campanha. Quem promove uma campanha para <see cref="Completed"/> ou
/// <see cref="Cancelled"/> e a API; o Worker apenas le a decisao ja tomada.
/// </summary>
/// <remarks>
/// Atingir a meta <b>nao</b> encerra a campanha: doar depois disso e permitido e ultrapassar o
/// valor e um desfecho normal. O Worker nunca escreve nesta coluna nem le <c>FinancialGoal</c>.
/// </remarks>
public enum CampaignStatus
{
    Active = 1,
    Completed = 2,
    Cancelled = 3,
}
