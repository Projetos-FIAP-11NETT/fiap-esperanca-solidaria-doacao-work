# fiap-esperanca-solidaria-doacao-work

Worker de **processamento de pagamento das doações** da plataforma Esperança Solidária (FIAP 11NETT).
Consome a fila SQS `process-donation-payment` (alimentada pela `campanha-api`), simula o pagamento,
atualiza o status da doação e credita o valor na campanha quando aprovado.

> Para subir o ambiente completo (k8s, LocalStack, API Gateway, front), siga o README do repositório
> **`fiap-esperanca-solidaria-infra`**. Este documento cobre o worker isoladamente.

---

## Sumário

- [Stack](#stack)
- [Como funciona](#como-funciona)
- [Estrutura da solução](#estrutura-da-solução)
- [Configuração](#configuração)
- [Rodando localmente](#rodando-localmente)
- [Rodando no Kubernetes](#rodando-no-kubernetes)
- [Endpoints de operação](#endpoints-de-operação)
- [Testes](#testes)
- [CI/CD](#cicd)
- [Troubleshooting](#troubleshooting)

---

## Stack

| Tecnologia | Uso |
|---|---|
| .NET 10 (`WebApplication` + `BackgroundService`) | Host do worker com endpoints de health/metrics |
| AWS SDK SQS | Long polling da fila de doações |
| Entity Framework Core + Npgsql | Acesso ao schema `fundraising` (mesmo banco da `campanha-api`) |
| OpenTelemetry + Prometheus | Traces (Tempo) e métricas (`/metrics`) |
| xUnit | Testes do handler |

---

## Como funciona

```
campanha-api ── {DonationId, CorrelationId} ──▶ SQS process-donation-payment
                                                        │ long polling (20 s, até 10 msgs)
                                                        ▼
                                          DonationPaymentWorker (BackgroundService)
                                                        │ 1 escopo DI + timeout por mensagem
                                                        ▼
                                          ProcessDonationPaymentHandler (1 transação)
   1. UPDATE Donation SET Status=PaymentProcessing WHERE Id=@id AND Status=Pending  ← porta de idempotência
        └─ 0 linhas? → mensagem repetida (AlreadyProcessed) ou doação inexistente (DonationNotFound)
   2. Campanha precisa estar Active, senão → Rejected
   3. RandomPaymentSimulator sorteia com Payments:ApprovalRate[forma de pagamento]
   4. Approved → Status=Approved + Campaigns.TotalRaised += Amount
      Rejected → Status=Rejected
   5. PaymentEvent gravado em cada passo (trilha de auditoria)
                                                        │
                           sucesso (qualquer desfecho) → DeleteMessage (ack)
                           exceção → rollback, sem ack → mensagem volta após o VisibilityTimeout
```

Pontos de projeto:

- **A mensagem só carrega ids.** Valor, campanha e forma de pagamento são lidos do banco, então uma
  mensagem adulterada ou antiga não consegue creditar valor diferente do real.
- **Idempotência** vem do `UPDATE ... WHERE Status = Pending`: o SQS é *at-least-once*; numa reentrega o
  update não casa e a doação nunca "muda de ideia".
- **Falha de infraestrutura = exceção.** Os desfechos `Approved`, `Rejected`, `AlreadyProcessed` e
  `DonationNotFound` confirmam a mensagem; qualquer exceção desfaz a transação, grava um `PaymentEvent`
  `Critical` e deixa a mensagem voltar para a fila.
- **Sem migrations aqui.** O schema `fundraising` (tabelas `Campaigns`, `Donation`, `PaymentEvent`) é
  criado pelas migrations da `campanha-api`; o `DbContext` deste repo só mapeia as colunas que usa e
  `EnsureCreated` nunca deve ser chamado.

---

## Estrutura da solução

Solução: `fiap-esperanca-solidaria-doacao-work.slnx`

```
src/
├── EsperancaSolidaria.Doacao.Domain/          # Entidades Campaign, Donation, PaymentEvent e enums
├── EsperancaSolidaria.Doacao.Application/
│   ├── Commands/ProcessDonationPaymentHandler.cs  # Regra de negócio (transação, idempotência, crédito)
│   ├── Commands/DonationReceivedEvent.cs          # Contrato da mensagem {DonationId, CorrelationId}
│   ├── Commands/PaymentProcessingOutcome.cs       # Desfechos conclusivos
│   └── Interfaces/                                # Repositórios, fila, simulador, unit of work
├── EsperancaSolidaria.Doacao.Infrastructure/
│   ├── Data/                                      # DbContext (schema fundraising), UnitOfWork, PaymentEventLogger, health check
│   ├── Messaging/                                 # SqsPaymentQueue, SqsOptions, health check do SQS
│   ├── Payments/RandomPaymentSimulator.cs         # "Adquirente" simulado
│   ├── Repositories/
│   └── DependencyInjection.cs                     # Registro de serviços, OpenTelemetry, /metrics
└── EsperancaSolidaria.Doacao.Worker/
    ├── DonationPaymentWorker.cs                   # Loop de polling, timeout, ack
    ├── Logging/DoacaoWorkerConsoleFormatter.cs    # Formato de log do console
    ├── Program.cs
    └── appsettings*.json
tests/EsperancaSolidaria.Doacao.Tests/             # Testes unitários do handler
docker/dockerfile                                   # Dockerfile usado pelo CD
Directory.Build.props                               # Propriedades comuns de build
```

---

## Configuração

| Chave | Padrão | Descrição |
|---|---|---|
| `ConnectionStrings:Postgres` | — | Banco da `campanha-api` com `Search Path=fundraising` |
| `Aws:Region` / `Aws:ServiceUrl` | `us-east-1` / vazio | `ServiceUrl` aponta para o LocalStack em dev; vazio = AWS real |
| `Sqs:QueueUrl` | — | URL da fila `process-donation-payment` |
| `Sqs:MaxNumberOfMessages` | `10` | Mensagens por receive |
| `Sqs:WaitTimeSeconds` | `20` | Long polling |
| `Sqs:VisibilityTimeoutSeconds` | `60` | Tempo até uma mensagem não confirmada reaparecer |
| `Worker:ProcessingTimeoutSeconds` | `30` | Teto de processamento de cada mensagem (> 0) |
| `Payments:ApprovalRate:<Método>` | Pix `0.98`, DebitCard `0.90`, CreditCard `0.85` | Probabilidade de aprovação por forma de pagamento |
| `OpenTelemetry:ServiceName` / `TempoEndpoint` | — | Traces |

Credenciais AWS vêm das variáveis padrão do SDK (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` —
`test`/`test` no LocalStack).

> **Atenção:** não há taxa configurada para `Boleto` (valor `4`). Uma doação com Boleto faz o simulador
> lançar exceção, a transação é desfeita e a mensagem volta para a fila indefinidamente. Adicione
> `"Boleto": <taxa>` em `Payments:ApprovalRate` se a forma de pagamento for oferecida no front.

---

## Rodando localmente

Pré-requisitos: .NET SDK 10, o Postgres com o schema já criado pela `campanha-api` e o LocalStack com a
fila. O `appsettings.Development.json` aponta para `localhost:5444` (Postgres do docker-compose da infra) e
`localhost:4566` (LocalStack).

```powershell
aws sqs create-queue --queue-name process-donation-payment --endpoint-url http://localhost:4566 --region us-east-1
$env:AWS_ACCESS_KEY_ID = "test"; $env:AWS_SECRET_ACCESS_KEY = "test"
dotnet run --project src/EsperancaSolidaria.Doacao.Worker
```

O host sobe em http://localhost:5000. Em Development o timeout por mensagem é de 1 h e o visibility
timeout de 10 min, para facilitar depuração com breakpoints.

Para testar sem a API, crie uma doação `Pending` no banco e envie a mensagem à mão:

```powershell
aws sqs send-message --endpoint-url http://localhost:4566 --region us-east-1 `
  --queue-url http://localhost:4566/000000000000/process-donation-payment `
  --message-body '{"DonationId":"<guid-da-doacao>","CorrelationId":"<qualquer-guid>"}'
```

### Docker

```powershell
docker build -f docker/dockerfile -t doacao-work:local .
```

---

## Rodando no Kubernetes

Deployment em `fiap-esperanca-solidaria-infra/k8s/donation-worker/` (imagem
`projetofiap/fiap-esperanca-solidaria-doacao-work:latest`, Service `donation-worker`, NodePort 30083 → 8080).
Recebe `Sqs__QueueUrl` (configmap `SQS_DONATION_QUEUE_URL`), `Aws__ServiceUrl` e
`ConnectionStrings__Postgres` (`DB_DOACAO_CONNECTION_STRING`).

```powershell
kubectl logs -n apps deploy/donation-deployment -f
kubectl rollout restart deployment/donation-deployment -n apps   # puxa a :latest mais recente
```

---

## Endpoints de operação

| Rota | Descrição |
|---|---|
| `/health/live` | Processo vivo (não verifica dependências) |
| `/health/ready` | Verifica Postgres e SQS |
| `/metrics` | Métricas Prometheus |

---

## Testes

```powershell
dotnet test fiap-esperanca-solidaria-doacao-work.slnx
```

`ProcessDonationPaymentHandlerTests` cobre aprovação com crédito, rejeição no sorteio, campanha
encerrada/inexistente, mensagem repetida, doação já em processamento por outra réplica, doação inexistente
e falha de infraestrutura (rollback + evento `Critical`).

---

## CI/CD

| Workflow | Gatilho | O que faz |
|---|---|---|
| `ci-push.yml` | push em `feature/**`, `bugfix/**`, `hotfix/**` | build + testes; abre PR para `Development` se não existir |
| `ci-pull-request.yml` | PR para `Development`/`main` | build + testes |
| `cd-release.yml` | tag `v*` | build, testes, Trivy e push de `:<tag>` e `:latest` para `projetofiap/fiap-esperanca-solidaria-doacao-work` |

> A branch de integração deste repo é **`Development`** (não `develop`).

---

## Troubleshooting

| Sintoma | Solução |
|---|---|
| Doações ficam `Pending` | Worker parado, `Sqs:QueueUrl` errado ou fila inexistente (LocalStack reiniciado → reaplicar o Terraform da infra). |
| Mesma mensagem reprocessada em loop | Exceção no handler — veja os logs e os `PaymentEvent` `Critical`. Causa comum: forma de pagamento sem taxa (Boleto). |
| `relation "fundraising.Donation" does not exist` | A `campanha-api` ainda não rodou as migrations nesse banco, ou falta `Search Path=fundraising`. |
| `/health/ready` falhando | Postgres ou SQS inacessíveis a partir do pod. |
