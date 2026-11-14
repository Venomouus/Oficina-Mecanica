 # Microsserviços da oficina

Esta pasta contém a primeira separação funcional da aplicação:

- `os-service`: abertura e acompanhamento da ordem de serviço, com PostgreSQL próprio.
- `billing-service`: geração e aprovação de orçamento, com MongoDB próprio.
- `execution-service`: entrada da OS paga na fila de execução, com banco MongoDB separado.

Os serviços não acessam o banco uns dos outros. A integração inicial usa eventos RabbitMQ no exchange `oficina.events`:

```text
OS_CREATED -> BUDGET_CREATED -> BUDGET_APPROVED -> PAYMENT_APPROVED
                                                     |
                                                     v
                                             execution-service
```

## Execução local

Na raiz do projeto:

```powershell
docker compose up --build os-db billing-db rabbitmq os-service billing-service execution-service
```

Interfaces disponíveis:

- OS Service: http://localhost:8081/swagger
- Billing Service: http://localhost:8082/swagger
- Execution Service: http://localhost:8083/swagger
- RabbitMQ: http://localhost:15672 (`oficina` / `oficina`)

Exemplo de fluxo:

```powershell
$order = Invoke-RestMethod -Method Post http://localhost:8081/api/orders -ContentType 'application/json' -Body (@{
  customerName = 'Cliente Demo'
  vehiclePlate = 'ABC1D23'
  description = 'Revisao de freios'
} | ConvertTo-Json)

Invoke-RestMethod http://localhost:8082/api/budgets
```

O orçamento criado pelo evento pode ser aprovado com `POST /api/budgets/{id}/approve` e o pagamento de demonstração pode ser confirmado com `POST /api/budgets/{id}/payment`. A integração real com o Mercado Pago será adicionada na próxima etapa, mantendo estes contratos para o fluxo local.
