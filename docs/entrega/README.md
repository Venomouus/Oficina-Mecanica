# Entrega verificavel — Tech Challenge

Infraestrutura provisionada no AWS Academy com EKS, RDS, Lambda e API Gateway.
Veja [evidencias reais AWS](evidencias-aws.json), [implantacao](../../academy/README.md)
e [arquitetura](arquitetura.md). A alternativa local foi preservada. As pendencias
abaixo permanecem explicitas; a entrega nao inclui um video inventado.

## Abrir a demonstracao

Na raiz da API, execute `powershell -ExecutionPolicy Bypass -File academy/access.ps1`.
O script abre Grafana, mostra a senha somente no terminal local e prepara o acesso
administrativo privado. Nao grave a senha. Depois execute `python academy/demo.py`.
Importe [a colecao Postman](Oficina-AWS.postman_collection.json) para mostrar o JWT
e as rotas protegidas. O CPF de demonstracao e ficticio: `12345678909`.

- Homologacao: https://tyza3bgnt9.execute-api.us-east-1.amazonaws.com
- Producao: https://5fzdgx69ni.execute-api.us-east-1.amazonaws.com
- Grafana via acesso local ao EKS: http://127.0.0.1:13000/d/oficina-local

## Antes de enviar

1. Mantenha a sessao Academy, Docker e os runners ativos; consulte [academy/README.md](../../academy/README.md).
2. Abra API, autenticador, dashboard e alertas; confira o relatorio gerado pelo script.
3. Publique as alteracoes por PR: feature/entrega-local-observabilidade -> develop -> master.
   Aguarde os checks do commit mais recente. Nao confunda CI verde com deploy AWS.
4. Confira em cada repositorio se `soat-architecture` recebeu acesso; aceite pendente
   deve ser descrito como convite pendente, nunca como acesso confirmado.
5. Grave o video abaixo, publique no YouTube/Vimeo e copie o link.
6. Preencha `dados-entrega.json` com o video e a situacao verdadeira do avaliador;
   gere novamente o PDF com `python docs/entrega/gerar_pdf.py` e envie o PDF ao portal.

O PDF inicial e um rascunho honesto, com video/acesso ainda pendentes. Esses dois
dados dependem da sua conta; nao foram preenchidos com informacoes inventadas.

## Roteiro do video (ate 15 minutos)

| Tempo | Demonstracao |
|---|---|
| 0:00–1:00 | Objetivo, quatro repositorios, diagrama AWS e limites do laboratorio |
| 1:00–2:30 | PRs e Actions: build, testes, validacao Terraform e teste Kind existentes |
| 2:30–4:00 | EKS com pods/HPA, RDS, Lambda e API Gateway; mostrar uma execucao CD real |
| 4:00–7:00 | CPF -> JWT -> abrir/aprovar OS; outro cliente recebe 404; cliente nao acessa admin |
| 7:00–9:00 | `python academy/demo.py`; volume diario e medias reais por status no Grafana |
| 9:00–11:30 | CPU/memoria dos pods, healthchecks/uptime e regras de alerta de falha de OS |
| 11:30–13:00 | Loki: log JSON com trace_id; Tempo: pesquisar trace correspondente |
| 13:00–14:30 | Diagramas, decisoes, ER, Terraform e limites/notificacoes externas pendentes |

Use dados de demonstracao. Nao exponha JWT completo, chaves ou credenciais de contas
reais. As senhas publicas locais sao somente para este laboratorio. Mostre uma
execucao real da pipeline; se nao houver CD cloud, declare essa limitacao no video.

## Estado dos requisitos

| Bloco | Evidencia disponivel | Limite |
|---|---|---|
| Autenticacao CPF/JWT e autorizacao | Lambda/RDS/Gateway/API demonstrados em homologacao | Evidencias em evidencias-aws.json |
| Quatro repositorios e CI/CD | Master protegida e avaliador com acesso confirmados; workflows CD/runners configurados | Publicar/promover PRs e registrar execucao CD verde |
| Terraform/RDS/EKS/Gateway | Recursos provisionados na conta Academy | Um cluster/RDS compartilhado por dois ambientes; RDS Single-AZ |
| Observabilidade | OTLP, Grafana/Prometheus/Loki/Tempo, alertas, cAdvisor e sondas HTTP no EKS | Dados temporarios; sem envio externo de alertas |
| Negocio | OS por dia; medias de periodos concluidos, inclusive Diagnostico | Janela UTC; nao representa carga real de producao |
| Logs e traces | Logs JSON correlacionados e traces da API | Autenticador tem logs proprios, sem OTLP integrado |
| Notificacoes | Contrato/projeto existentes no serverless | Envio/eventos/serverless ponta a ponta pendentes |
| Documentacao | Arquitetura cloud/local, sequencias, ER, decisoes e instrucoes | Links devem apontar para branches publicadas |
| Video e PDF | Roteiro, dados editaveis e gerador PDF | Usuario precisa gravar/publicar video e confirmar acesso |

## Repositorios

- https://github.com/Venomouus/Oficina-Mecanica
- https://github.com/Venomouus/Oficina-serverless
- https://github.com/Venomouus/Oficina-infra-kubernetes
- https://github.com/Venomouus/Oficina-infra-database

Esta documentacao fica no repositorio principal para centralizar a avaliacao.
Os READMEs dos demais repositorios incluem o modo Academy e suas limitacoes.

## Evidencia da demonstracao executada

Veja [evidencias-execucao.json](evidencias-execucao.json): autenticacao CPF, OS,
autorizacao, periodos reais, metricas, logs e traces observados no ambiente local.
O Grafana carregou 13 paineis e duas regras com health `ok`. Disparo e recuperacao
dos alertas nao foram ensaiados nesta rodada; o procedimento esta no roteiro.

## Publicar os arquivos desta entrega

As alteracoes da API usam a branch `feature/entrega-local-observabilidade`; os outros
repositorios usam `feature/academy-deploy`. Promover por PR para develop e depois master.
Na raiz da API, os comandos abaixo servem apenas se ainda houver mudancas nao publicadas:

```powershell
git add .dockerignore Oficina.API Oficina.Application/Services/OrdemServicoService.cs Oficina.Tests/Integration README.md docs local
git commit -m "feat: preparar demonstracao local e documentacao do tech challenge"
git push -u origin feature/entrega-local-observabilidade
```

Crie PR com **base develop** e **compare feature/entrega-local-observabilidade**.
Depois dos checks e merge, promova **base master / compare develop**. O PDF referencia
a master; abra os links depois da promocao. Nao inclua chaves, JWTs ou arquivos locais
ignorados no commit. Sugestao de titulo: `Adicionar observabilidade e entrega local do Tech Challenge`.
