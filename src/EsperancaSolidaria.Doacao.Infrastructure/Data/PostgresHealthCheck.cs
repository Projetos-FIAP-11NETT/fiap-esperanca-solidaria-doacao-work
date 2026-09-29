using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EsperancaSolidaria.Doacao.Infrastructure.Data;

/// <summary>
/// Check de readiness do Postgres. Registrado com a tag <c>ready</c>, entra apenas em
/// <c>/health/ready</c>: se o banco cair, o pod sai do trafego mas nao e reiniciado.
/// </summary>
internal sealed class PostgresHealthCheck(EsperancaSolidariaDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Postgres respondeu.")
                : HealthCheckResult.Unhealthy("Postgres nao respondeu.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Falha ao consultar o Postgres.", exception);
        }
    }
}
