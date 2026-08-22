using Amazon;
using Amazon.SQS;

using EsperancaSolidaria.Doacao.Application.Interfaces;
using EsperancaSolidaria.Doacao.Application.Commands;
using EsperancaSolidaria.Doacao.Infrastructure.Messaging;
using EsperancaSolidaria.Doacao.Infrastructure.Payments;
using EsperancaSolidaria.Doacao.Infrastructure.Data;
using EsperancaSolidaria.Doacao.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EsperancaSolidaria.Doacao.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Liga as ports da Application as implementacoes concretas. E o unico lugar do sistema
    /// que sabe, ao mesmo tempo, que existem Postgres e SQS.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ValidateConfiguration(configuration);

        services.Configure<SqsOptions>(configuration.GetSection(SqsOptions.SectionName));
        services.Configure<PaymentSimulationOptions>(
            configuration.GetSection(PaymentSimulationOptions.SectionName));

        AddPersistence(services, configuration);
        AddMessaging(services, configuration);

        services.AddScoped<IPaymentSimulator, RandomPaymentSimulator>();
        services.AddScoped<ProcessDonationPaymentHandler>();

        // Somente readiness: /health/live nao consulta dependencia nenhuma, senao um Postgres
        // fora do ar faria o Kubernetes reiniciar os pods em vez de tira-los do trafego.
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"])
            .AddCheck<SqsHealthCheck>("sqs", tags: ["ready"]);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");

        // A factory atende quem precisa de uma conexao propria (o registro do PaymentEvent
        // Critical, que roda depois do rollback); o escopo continua tendo um contexto so.
        services.AddDbContextFactory<EsperancaSolidariaDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped(provider =>
            provider.GetRequiredService<IDbContextFactory<EsperancaSolidariaDbContext>>()
                .CreateDbContext());

        services.AddScoped<IDonationRepository, DonationRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<IPaymentEventRepository, PaymentEventRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IPaymentEventLogger, PaymentEventLogger>();
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        var region = configuration["Aws:Region"];
        var serviceUrl = configuration["Aws:ServiceUrl"];

        services.AddSingleton<IAmazonSQS>(_ =>
        {
            var config = new AmazonSQSConfig();

            if (!string.IsNullOrWhiteSpace(serviceUrl))
            {
                // LocalStack. Preenchido, este e o unico interruptor entre o ambiente local e
                // a AWS de verdade.
                config.ServiceURL = serviceUrl;
                config.AuthenticationRegion = region;
            }
            else if (!string.IsNullOrWhiteSpace(region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
            }

            // Sem credencial no codigo: o SDK resolve pela cadeia padrao (variaveis de
            // ambiente, perfil, role do pod).
            return new AmazonSQSClient(config);
        });

        services.AddSingleton<IPaymentQueue, SqsPaymentQueue>();
    }

    /// <summary>
    /// Falha no arranque em vez de na primeira mensagem: configuracao errada aparece no log
    /// do pod, nao em uma doacao perdida.
    /// </summary>
    private static void ValidateConfiguration(IConfiguration configuration)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")))
        {
            errors.Add("ConnectionStrings:Postgres nao foi configurada.");
        }

        var sqsOptions = configuration.GetSection(SqsOptions.SectionName).Get<SqsOptions>()
            ?? new SqsOptions();
        errors.AddRange(sqsOptions.Validate());

        var paymentOptions = configuration
            .GetSection(PaymentSimulationOptions.SectionName)
            .Get<PaymentSimulationOptions>() ?? new PaymentSimulationOptions();
        errors.AddRange(paymentOptions.Validate());

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Configuracao invalida:{Environment.NewLine} - {string.Join($"{Environment.NewLine} - ", errors)}");
        }
    }
}
