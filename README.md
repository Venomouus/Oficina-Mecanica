# Oficina Mecanica API

MVP de back-end monolitico em ASP.NET Core 8 para gestao de clientes, veiculos, servicos, pecas/insumos e ordens de servico de uma oficina mecânica.

## Arquitetura

- `Oficina.Domain`: entidades, regras de status da OS, estoque e validadores de CPF/CNPJ e placa.
- `Oficina.Infrastructure`: persistencia com EF Core e PostgreSQL.
- `Oficina.API`: controllers REST, autenticacao JWT e Swagger.
- `Oficina.Tests`: testes unitarios dos dominios criticos.

## Banco de dados

Foi escolhido PostgreSQL por ser relacional, robusto, gratuito e adequado para dados transacionais da oficina: clientes, veiculos, estoque, itens da OS e 
historico precisam de integridade referencial, indices unicos e consultas administrativas consistentes.

## Executar com Docker

```powershell
docker compose up --build
```

Swagger:

```text
http://localhost:8080/swagger
```

Credenciais administrativas do MVP:

```json
{
  "usuario": "admin",
  "senha": "Admin@123"
}
```

Obtenha o token em `POST /api/auth/login` e use o botao `Authorize` do Swagger com o valor `Bearer {token}`.

## Executar localmente

Suba um PostgreSQL local e ajuste `Oficina.API/appsettings.json` se necessario. Depois:

```powershell
dotnet restore
dotnet run --project Oficina.API/Oficina.API.csproj
```

## Endpoints principais

- `POST /api/auth/login`: gera JWT administrativo.
- `GET|POST|PUT|DELETE /api/clientes`: CRUD de clientes.
- `GET|POST|PUT|DELETE /api/veiculos`: CRUD de veiculos.
- `GET|POST|PUT|DELETE /api/servicos`: CRUD de servicos.
- `GET|POST|PUT|DELETE /api/pecas-insumos`: CRUD e estoque.
- `POST /api/ordens-servico`: cria OS, calcula orcamento e muda para `AguardandoAprovacao`.
- `PATCH /api/ordens-servico/{id}/status`: avanca status administrativo.
- `POST /api/ordens-servico/{id}/aprovar?cpfCnpj=...`: aprovacao pelo cliente.
- `GET /api/ordens-servico/consulta/{id}?cpfCnpj=...`: acompanhamento pelo cliente.
- `GET /api/ordens-servico/metricas/tempo-medio`: tempo medio de execucao.

## Testes e cobertura

```powershell
dotnet test --collect:"XPlat Code Coverage"
```

Os testes cobrem validacao de documentos, placas, fluxo de status/orcamento da OS e controle de estoque.

## Observacoes de seguranca

Foi executado um scan de vulnerabilidades com a ferramenta Snyk CLI nas dependências NuGet da solução OficinaMecanica.sln.

Comando executado:
snyk test --file=OficinaMecanica.sln --json > snyk-report.json

Resultado:
A análise não identificou vulnerabilidades conhecidas nas dependências dos projetos Oficina.API, Oficina.Domain, Oficina.Application, Oficina.Infrastructure e Oficina.Tests.

Resumo do Snyk:
- Status: aprovado
- Vulnerabilidades encontradas: 0
- Projetos analisados: 5
- Package manager: NuGet
