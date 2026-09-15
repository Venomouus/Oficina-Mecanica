# Arquitetura e modelagem da entrega

## Componentes executados localmente

```mermaid
flowchart LR
    U[Swagger / script de demonstracao] --> A[API ASP.NET Core]
    U --> F[Host local da autenticacao CPF]
    F --> P[(PostgreSQL local)]
    A -->|JWT RSA: discovery e JWKS| F
    A --> P
    A -->|OTLP: metricas, logs e traces| O[OpenTelemetry Collector]
    O --> M[Prometheus]
    O --> L[Loki]
    O --> T[Tempo]
    G[Grafana: dashboard e alertas] --> M
    G --> L
    G --> T
```

O PostgreSQL desse Compose usa credencial administrativa exclusivamente para facilitar
a demonstracao. A segregacao app/auth/migrations e o bootstrap de privilegios do
repositorio de banco sao artefatos separados; nao se deve atribuir esses privilegios
restritos a este Compose. As migracoes locais sao aplicadas em Development; em EKS
existe Job separado, com migracoes automaticas no startup desabilitadas.

## Arquitetura implantada no AWS Academy

```mermaid
flowchart LR
    C[Cliente] --> GW[API Gateway HTTP / JWT]
    GW --> LA[Lambda autenticacao CPF]
    GW --> VL[VPC Link e backend privado]
    VL --> E[EKS: API / HPA]
    LA --> R[(RDS PostgreSQL privado)]
    E --> R
    S[Secrets Manager] --> LA
    S --> SY[Sincronizacao pelo operador/CD]
    SY --> KS[Secrets Kubernetes por finalidade]
    KS --> E
    E -->|OTLP| O[Grafana / Prometheus / Loki / Tempo no EKS]
    O -->|cAdvisor e sondas HTTP| E
    LA -. eventos: pendente .-> N[Notificacoes serverless: pendente]
```

EKS, RDS, Lambda, Gateway e monitoramento foram provisionados no Academy. O fluxo
de homologacao foi demonstrado; veja `evidencias-aws.json`. LabRole e reutilizada
pelos servicos permitidos no laboratorio. API/migrations recebem apenas seus segredos
montados, sem credenciais AWS. Homologacao e producao usam namespaces e bancos logicos
separados no mesmo cluster/RDS. O monitoramento usa armazenamento temporario de demo.

```mermaid
sequenceDiagram
    actor Cliente
    participant Gateway as API Gateway
    participant Lambda as Lambda CPF
    participant Banco as RDS
    participant API as API no EKS
    Cliente->>Gateway: POST /auth/cpf
    Gateway->>Lambda: Invocar autenticacao
    Lambda->>Banco: Consultar CPF e status ativo
    Banco-->>Lambda: Cliente
    Lambda-->>Cliente: JWT assinado
    Cliente->>Gateway: POST /api/minhas-ordens-servico + JWT
    Gateway->>Gateway: Validar JWT com JWKS
    Gateway->>API: VPC Link / ALB privado
    API->>API: Validar token e autorizacao do cliente
    API->>Banco: Persistir OS e historico
    API-->>Cliente: 201 + X-Correlation-ID
```

## Sequencia local de autenticacao e abertura de OS

```mermaid
sequenceDiagram
    actor Cliente
    participant Auth as Autenticador CPF
    participant DB as PostgreSQL
    participant API as API Oficina
    participant Obs as Collector / Grafana
    Cliente->>Auth: POST /auth/cpf
    Auth->>Auth: Validar formato e digitos do CPF
    Auth->>DB: Consultar cliente existente e ativo
    DB-->>Auth: Cliente
    Auth-->>Cliente: JWT RS256
    Cliente->>API: POST /api/minhas-ordens-servico + Bearer
    API->>Auth: Discovery/JWKS (cache de chaves)
    API->>API: Validar assinatura, issuer, audience e validade
    API->>DB: Conferir cliente ativo e persistir OS/historico
    API-->>Cliente: 201 Aguardando Aprovacao + X-Correlation-ID
    API->>Obs: Metricas, log e trace
    Cliente->>API: POST /api/minhas-ordens-servico/{id}/aprovar
    API->>DB: Conferir proprietario e transicionar para Execucao
    API-->>Cliente: Orcamento aprovado
    Note over Cliente,API: Outro cliente recebe 404 ao consultar a OS
```

## Modelo relacional

```mermaid
erDiagram
    Clientes ||--o{ Veiculos : possui
    Clientes ||--o{ OrdensServico : solicita
    Veiculos ||--o{ OrdensServico : recebe
    OrdensServico ||--o{ OrdemServicoServicos : inclui
    Servicos ||--o{ OrdemServicoServicos : referencia
    OrdensServico ||--o{ OrdemServicoPecas : consome
    PecasInsumos ||--o{ OrdemServicoPecas : referencia
    OrdensServico ||--|{ HistoricoStatusOrdemServico : registra
```

Os nomes dos itens no diagrama representam entidades de associacao; o mapeamento
fisico exato esta em `Oficina.Infrastructure/Persistence/OficinaDbContext.cs`.
Cliente/veiculo/catalogos sao referenciados por chaves estrangeiras. CPF e placa
possuem unicidade. Itens da OS preservam valores do orcamento, evitando que alteracoes
posteriores do catalogo mudem o historico financeiro. O historico usa sequencia unica
por OS e datas UTC; o agregado fecha/abre periodos numa mesma persistencia.

O PostgreSQL foi escolhido por transacoes ACID, integridade referencial, indices e
consultas relacionais adequadas a clientes, estoque e OS. RDS e a opcao gerenciada
preparada para backups/operacao futura, mas o banco local nao e gerenciado. Os testes
nao substituem benchmark de producao. A consulta de observabilidade agrega sete dias
de criacao e periodos encerrados em 24h; para volume grande, avaliar EXPLAIN, indices
e agregacao incremental antes de reduzir intervalos. A concorrencia da OS usa Versao;
disputa de estoque entre OS distintas permanece uma limitacao conhecida.

## Decisoes e referencias

- [ADR de observabilidade local](../adrs/003-observabilidade-local.md).
- [ADR de HPA e comunicacao](../adrs/004-hpa-comunicacao.md).
- [RFC da entrega local e escolhas de plataforma](rfc-entrega-local.md).
- [Historico e concorrencia](../adrs/001-historico-status-os.md).
- [Autorizacao de clientes](../adrs/002-autorizacao-cliente-jwt.md).
- [Modelagem temporal detalhada](../modelagem-status-historico.md).
- [Deploy EKS e migrations](../deploy-eks.md).
- RFCs e ADRs de plataforma/RDS/Gateway nos repositorios de infraestrutura.
