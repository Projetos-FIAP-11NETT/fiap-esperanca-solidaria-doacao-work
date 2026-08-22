using EsperancaSolidaria.Doacao.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace EsperancaSolidaria.Doacao.Infrastructure.Data;

/// <summary>
/// Mapeia o banco <c>EsperancaSolidaria</c>, compartilhado com a API de campanhas.
/// </summary>
/// <remarks>
/// A API e a dona do schema: este contexto so descreve tabelas que ja existem. Nao ha
/// migrations neste repositorio e <c>EnsureCreated</c> nunca deve ser chamado.
/// </remarks>
public sealed class EsperancaSolidariaDbContext(DbContextOptions<EsperancaSolidariaDbContext> options)
    : DbContext(options)
{
    public DbSet<Donation> Donations => Set<Donation>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EsperancaSolidariaDbContext).Assembly);
}
