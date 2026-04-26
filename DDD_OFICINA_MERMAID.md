# DDD - Oficina Mecanica

Este arquivo esta em formato Mermaid. Voce pode usar em:

- Lucidchart: importar/colar Mermaid, se a opcao estiver disponivel na sua conta.
- diagrams.net/draw.io: `Arrange > Insert > Advanced > Mermaid`.
- Mermaid Live Editor: https://mermaid.live
- VS Code: extensao `Markdown Preview Mermaid Support`.

## 1. Context Map

```mermaid
flowchart LR
    ClienteApp[Cliente / Aplicativo] -->|Consulta status da OS| Atendimento
    ClienteApp -->|Aprova orcamento| Atendimento

    Admin[Administrador da Oficina] -->|Gerencia cadastros| Administrativo
    Mecanico[Mecanico] -->|Executa diagnostico e servicos| Execucao

    subgraph Sistema[Monolito - Sistema Integrado da Oficina]
        Atendimento[Contexto: Atendimento]
        Execucao[Contexto: Execucao de Servicos]
        Estoque[Contexto: Estoque]
        Administrativo[Contexto: Administrativo]
    end

    Atendimento -->|Cria Ordem de Servico| Execucao
    Atendimento -->|Solicita orcamento| Execucao
    Execucao -->|Consulta e baixa pecas| Estoque
    Administrativo -->|Mantem clientes, veiculos, servicos e pecas| Atendimento
    Administrativo -->|Monitora tempo medio| Execucao
```

## 2. Linguagem Ubiqua

```mermaid
mindmap
  root((Oficina Mecanica))
    Cliente
      CPF/CNPJ
      Nome
      Telefone
      Email
    Veiculo
      Placa
      Marca
      Modelo
      Ano
    Ordem de Servico
      Numero
      Status
      Orcamento
      Aprovacao
      Entrega
    Servico
      Nome
      Preco
      Tempo estimado
    Peca/Insumo
      Codigo
      Preco unitario
      Estoque
      Estoque minimo
```

## 3. Modelo de Dominio

```mermaid
classDiagram
    class Cliente {
        +Guid Id
        +string Nome
        +string CpfCnpj
        +string Telefone
        +string Email
    }

    class Veiculo {
        +Guid Id
        +string Placa
        +string Marca
        +string Modelo
        +int Ano
        +Guid ClienteId
    }

    class OrdemServico {
        +Guid Id
        +string Numero
        +StatusOrdemServico Status
        +decimal ValorTotal
        +DateTime CriadaEm
        +EnviarParaAprovacao()
        +Aprovar()
        +IniciarExecucao()
        +Finalizar()
        +Entregar()
    }

    class Servico {
        +Guid Id
        +string Nome
        +string Descricao
        +decimal Preco
        +int TempoEstimadoMinutos
        +bool Ativo
    }

    class PecaInsumo {
        +Guid Id
        +string Nome
        +string Codigo
        +decimal PrecoUnitario
        +int QuantidadeEstoque
        +BaixarEstoque()
        +ReporEstoque()
    }

    class OrdemServicoServico {
        +Guid ServicoId
        +string Nome
        +decimal ValorUnitario
        +int TempoEstimadoMinutos
    }

    class OrdemServicoPeca {
        +Guid PecaInsumoId
        +string Nome
        +int Quantidade
        +decimal ValorUnitario
    }

    Cliente "1" --> "0..*" Veiculo
    Cliente "1" --> "0..*" OrdemServico
    Veiculo "1" --> "0..*" OrdemServico
    OrdemServico "1" --> "1..*" OrdemServicoServico
    OrdemServico "1" --> "0..*" OrdemServicoPeca
    OrdemServicoServico "*" --> "1" Servico
    OrdemServicoPeca "*" --> "1" PecaInsumo
```

## 4. Estados da Ordem de Servico

```mermaid
stateDiagram-v2
    [*] --> Recebida
    Recebida --> EmDiagnostico: iniciar diagnostico
    Recebida --> AguardandoAprovacao: gerar/enviar orcamento
    EmDiagnostico --> AguardandoAprovacao: finalizar diagnostico e orcar
    AguardandoAprovacao --> EmExecucao: cliente aprova
    EmDiagnostico --> EmExecucao: execucao liberada internamente
    EmExecucao --> Finalizada: servicos concluidos
    Finalizada --> Entregue: veiculo entregue
    Entregue --> [*]
```

## 5. Event Storming - Criacao e Acompanhamento da OS

```mermaid
flowchart TD
    A[Comando: Identificar cliente por CPF/CNPJ] --> B[Evento: Cliente identificado]
    B --> C[Comando: Cadastrar ou localizar veiculo]
    C --> D[Evento: Veiculo vinculado a OS]
    D --> E[Comando: Incluir servicos solicitados]
    E --> F[Evento: Servicos adicionados]
    F --> G[Comando: Incluir pecas e insumos previstos]
    G --> H[Evento: Pecas adicionadas ao orcamento]
    H --> I[Politica: Calcular valor total]
    I --> J[Evento: Orcamento gerado]
    J --> K[Comando: Enviar orcamento ao cliente]
    K --> L[Evento: OS aguardando aprovacao]
    L --> M[Comando: Cliente aprova orcamento]
    M --> N[Evento: Orcamento aprovado]
    N --> O[Politica: Baixar estoque]
    O --> P[Evento: OS em execucao]
    P --> Q[Comando: Finalizar servico]
    Q --> R[Evento: OS finalizada]
    R --> S[Comando: Entregar veiculo]
    S --> T[Evento: OS entregue]
```

## 6. Event Storming - Gestao de Pecas e Insumos

```mermaid
flowchart TD
    A[Comando: Cadastrar peca/insumo] --> B[Evento: Peca/Insumo cadastrado]
    B --> C[Comando: Repor estoque]
    C --> D[Evento: Estoque atualizado]
    D --> E[Politica: Verificar estoque minimo]
    E --> F{Estoque abaixo do minimo?}
    F -->|Sim| G[Evento: Alerta de reposicao necessario]
    F -->|Nao| H[Evento: Estoque adequado]
    H --> I[Comando: Usar peca em OS aprovada]
    G --> I
    I --> J{Estoque suficiente?}
    J -->|Sim| K[Evento: Estoque baixado]
    J -->|Nao| L[Evento: Execucao bloqueada por falta de estoque]
```

## 7. Fluxo de Autenticacao Administrativa

```mermaid
sequenceDiagram
    actor Admin
    participant API
    participant JWT
    participant Recursos as APIs Administrativas

    Admin->>API: POST /api/auth/login
    API->>API: Valida usuario e senha
    API->>JWT: Gera token assinado
    JWT-->>API: Token JWT
    API-->>Admin: accessToken
    Admin->>Recursos: Requisicao com Bearer Token
    Recursos->>Recursos: Valida token
    Recursos-->>Admin: Resposta autorizada
```

## 8. Fluxo de Consulta e Aprovacao pelo Cliente

```mermaid
sequenceDiagram
    actor Cliente
    participant API
    participant OS as Ordem de Servico
    participant Estoque

    Cliente->>API: GET /api/ordens-servico/consulta/{id}?cpfCnpj=...
    API->>OS: Busca OS e valida documento
    OS-->>API: Status e orcamento
    API-->>Cliente: Dados da OS

    Cliente->>API: POST /api/ordens-servico/{id}/aprovar?cpfCnpj=...
    API->>OS: Valida documento e status
    OS->>OS: Aprovar()
    API->>Estoque: Baixar pecas/insumos
    Estoque-->>API: Estoque atualizado
    API-->>Cliente: OS em execucao
```

