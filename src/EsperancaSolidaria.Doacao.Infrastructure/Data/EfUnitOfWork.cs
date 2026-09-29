using EsperancaSolidaria.Doacao.Application.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EsperancaSolidaria.Doacao.Infrastructure.Data;

/// <summary>
/// Transacao explicita do EF Core. E scoped, como o <see cref="EsperancaSolidariaDbContext"/>:
/// cada mensagem processada tem o seu escopo de DI e, portanto, a sua transacao.
/// </summary>
internal sealed class EfUnitOfWork(EsperancaSolidariaDbContext context) : IUnitOfWork, IAsyncDisposable
{
    private IDbContextTransaction? transaction;

    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            throw new InvalidOperationException("Ja existe uma transacao aberta neste escopo.");
        }

        transaction = await context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (transaction is null)
        {
            throw new InvalidOperationException("Nao ha transacao aberta para confirmar.");
        }

        await transaction.CommitAsync(cancellationToken);
        await DiscardAsync();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (transaction is null)
        {
            return;
        }

        await transaction.RollbackAsync(cancellationToken);
        await DiscardAsync();
    }

    public async ValueTask DisposeAsync() => await DiscardAsync();

    private async ValueTask DiscardAsync()
    {
        if (transaction is null)
        {
            return;
        }

        var current = transaction;
        transaction = null;
        await current.DisposeAsync();
    }
}
