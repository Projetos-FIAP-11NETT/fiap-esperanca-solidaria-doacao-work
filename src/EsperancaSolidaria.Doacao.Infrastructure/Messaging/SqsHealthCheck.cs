using Amazon.SQS;
using Amazon.SQS.Model;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace EsperancaSolidaria.Doacao.Infrastructure.Messaging;

/// <summary>
/// Check de readiness da fila: pergunta os atributos da propria fila configurada, o que
/// valida de uma vez o endpoint, as credenciais e a existencia da <c>QueueUrl</c>.
/// </summary>
internal sealed class SqsHealthCheck(IAmazonSQS sqs, IOptions<SqsOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await sqs.GetQueueAttributesAsync(
                new GetQueueAttributesRequest
                {
                    QueueUrl = options.Value.QueueUrl,
                    AttributeNames = ["QueueArn"],
                },
                cancellationToken);

            return HealthCheckResult.Healthy("Fila SQS acessivel.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Falha ao consultar a fila SQS.", exception);
        }
    }
}
