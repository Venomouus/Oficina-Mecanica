# Relatorio de Vulnerabilidades

## Objetivo

Este relatorio apresenta uma avaliacao de seguranca do MVP da Oficina Mecanica API. A analise considera o codigo-fonte, as dependencias NuGet utilizadas no projeto, 
a autenticacao, a validacao de dados sensiveis e alguns riscos esperados para uma primeira versao de back-end.


## Escopo analisado

Foram considerados os seguintes projetos da solucao:

- `Oficina.API`
- `Oficina.Application`
- `Oficina.Domain`
- `Oficina.Infrastructure`
- `Oficina.Tests`

Tambem foram avaliados os arquivos de configuracao, a autenticacao JWT, a persistencia com Entity Framework Core e PostgreSQL, os validadores de CPF/CNPJ e placa, 
e as rotas publicas de consulta e aprovacao da ordem de servico.

## Scan com Snyk

Foi executado um scan de vulnerabilidades com a ferramenta Snyk CLI para analisar as dependencias NuGet da solucao.

Comando executado:

```powershell
snyk test --file=OficinaMecanica.sln --json > snyk-report.json
```

Resultado do scan:

- Projetos analisados: 5
- Gerenciador de pacotes: NuGet
- Vulnerabilidades conhecidas encontradas: 0
- Status do Snyk: aprovado

O arquivo gerado foi `snyk-report.json`. Em todos os projetos analisados, o Snyk retornou `ok: true`, `uniqueCount: 0` e `summary: No known vulnerabilities`. 
Isso significa que nao foram encontradas vulnerabilidades conhecidas nas dependencias utilizadas no momento da analise.

## Testes e cobertura

Tambem foram executados testes automatizados com coleta de cobertura.

Comando utilizado:

```powershell
dotnet test OficinaMecanica.sln --collect:"XPlat Code Coverage"
```

Resultado registrado:

- 23 testes aprovados
- 0 falhas
- Cobertura de linhas: 89,82%

Os testes cobrem pontos importantes do dominio, como validacao de CPF/CNPJ, validacao de placa, regras de status da ordem de servico e controle de estoque de pecas e insumos.

## Pontos positivos identificados

- As APIs administrativas exigem autenticacao JWT.
- O Swagger esta configurado com suporte ao token Bearer.
- CPF/CNPJ e validado antes do cadastro e armazenado de forma normalizada.
- CPF/CNPJ e exibido formatado nas respostas da API, melhorando a leitura sem prejudicar a consistencia interna.
- A placa do veiculo e validada no fluxo de criacao da ordem de servico.
- A consulta e a aprovacao da OS pelo cliente exigem o documento vinculado a ordem.
- O uso de Entity Framework Core com LINQ reduz o risco de SQL Injection, pois as consultas sao parametrizadas.
- O controle de estoque impede baixa quando a quantidade disponivel e insuficiente.
- O banco PostgreSQL e utilizado com migrations do Entity Framework Core.
- O scan do Snyk nao encontrou vulnerabilidades conhecidas nas dependencias NuGet.

## Riscos identificados e recomendacoes

| Severidade | Risco | Impacto | Recomendacao |
| --- | --- | --- | --- |
| Alta | Credenciais administrativas e chave JWT configuradas em arquivos locais ou variaveis simples de ambiente | Em um ambiente real, o vazamento dessas informacoes permitiria acesso indevido as rotas administrativas | Em producao, usar secrets, variaveis protegidas no provedor de nuvem ou um cofre de segredos |
| Media | JWT sem refresh token e sem mecanismo de revogacao | Caso um token seja comprometido, ele continua valido ate expirar | Usar expiracao curta, refresh token e estrategia de revogacao em versoes futuras |
| Media | Consulta e aprovacao da OS usam CPF/CNPJ como fator principal de validacao | CPF/CNPJ pode ser conhecido por terceiros, permitindo tentativa de acesso indevido a uma OS | Adicionar token de acompanhamento da OS, autenticacao do cliente ou codigo temporario enviado por canal seguro |
| Media | Migrations executadas automaticamente no startup da aplicacao | Em producao, alteracoes no schema durante a inicializacao podem gerar indisponibilidade ou comportamento inesperado | Executar migrations em etapa controlada de deploy ou pipeline |
| Baixa | Ausencia de rate limit nas rotas publicas e de login | Pode permitir muitas tentativas de login, consulta ou aprovacao em curto periodo | Adicionar rate limiting no ASP.NET Core |
| Baixa | Ausencia de auditoria detalhada para acoes administrativas | Dificulta rastrear quem alterou status, estoque ou cadastros | Registrar usuario, data, acao realizada e dados relevantes em trilha de auditoria |
| Baixa | Swagger habilitado em ambiente de desenvolvimento | Adequado para o MVP, mas nao deve expor detalhes da API em producao sem controle | Manter Swagger apenas em desenvolvimento ou proteger seu acesso em ambientes publicados |

## Conclusao

O MVP atende aos principais requisitos de seguranca esperados para a entrega academica. As rotas administrativas estao protegidas por JWT, os dados sensiveis possuem validacao, 
as regras de estoque evitam inconsistencias e o scan do Snyk nao encontrou vulnerabilidades conhecidas nas dependencias NuGet.

