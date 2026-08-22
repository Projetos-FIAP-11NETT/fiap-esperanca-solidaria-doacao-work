namespace EsperancaSolidaria.Doacao.Application.Interfaces;

/// <summary>
/// Transacao unica do processamento: a promocao para <c>PaymentProcessing</c>, o desfecho,
/// o credito na campanha e os <c>PaymentEvent</c> commitam juntos ou nao acontecem.
/// </summary>
public interface IUnitOfWork
{
    Task BeginAsync(CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}
