# Avaliacao do roteiro do Tech Challenge

## Atualizacao: entrega local

A avaliacao abaixo e o registro anterior a implementacao de observabilidade.
O estado atual e mantido em [docs/entrega/README.md](entrega/README.md).
Foram acrescentados Compose local, instrumentacao OpenTelemetry, Grafana/Prometheus/
Loki/Tempo, dashboard, regras de alerta, abertura administrativa em Diagnostico,
diagramas, RFC/ADRs e gerador de PDF. A demonstracao de autenticacao/OS e chegada de
metricas/logs/traces foi executada localmente. As regras de alerta foram carregadas;
seu ensaio de disparo/recuperacao esta disponivel no roteiro, sem alegar validacao
cloud. Video e acesso do avaliador continuam dependendo do usuario.

## Registro da avaliacao anterior (historico)


Data: 2026-09-14. Referencia: enunciado integral fornecido pelo usuario nesta conversa.
Escopo: leitura dos quatro checkouts locais, workflows e documentacao, resultados
de testes ja executados e capturas de PRs/demonstracao fornecidas pelo usuario.
Nao foi feita auditoria autenticada das configuracoes do GitHub ou da conta AWS.
Os checkouts locais estao em branches de trabalho; os prints confirmam as promocoes
apresentadas pelo usuario, mas nao substituem uma consulta atual de todos os remotos.

## Situacao geral

O projeto tem implementacao funcional local de autenticacao/OS, quatro repositorios,
CI e infraestrutura escrita em Terraform. A entrega ainda nao esta pronta para o
video final: faltam operacao em nuvem, deploy automatico de homologacao/producao,
observabilidade e complementacao da documentacao/evidencias. Checks verdes e
Terraform com mocks nao comprovam que os recursos estao provisionados.

## Autenticacao, repositorios e infraestrutura

| Requisito | Estado verificado | Evidencia ou pendencia |
| --- | --- | --- |
| API Gateway | Preparado em codigo | Root gateway no infra-kubernetes; falta provisionar e testar roteamento/JWT real |
| Rotas sensiveis protegidas via CPF/JWT | Implementado e testado localmente | Emissor CPF + API com RS256, perfis e restricao de OS ao cliente |
| Function valida CPF | Implementado localmente | Oficina.Autenticacao e testes |
| Function consulta existencia e status | Implementado localmente | Consulta PostgreSQL e cliente ativo; API tambem verifica Ativo |
| Function devolve JWT | Implementado localmente | Chave RSA, discovery/JWKS e testes de consumo |
| Function Serverless em nuvem | Pendente | Codigo/empacotamento/Terraform existem, sem implantacao comprovada |
| Quatro repositorios separados | Atendido | Oficina-Mecanica, Oficina-serverless, Oficina-infra-kubernetes e Oficina-infra-database |
| CI nos quatro repositorios | Implementada | Workflows de build, testes e validacao; execucoes apresentadas nos PRs |
| CD automatico de homologacao e producao nos quatro | Pendente | Workflows de infra/serverless sao CI; build/push de imagem e teste Kind nao equivalem a deploy cloud |
| Master protegida contra commits diretos | Nao verificado | Necessario conferir rulesets/branch protection e bypass no GitHub |
| PR obrigatorio | Uso observado, regra nao verificada | PRs apresentados confirmam o fluxo seguido, nao a obrigatoriedade configurada |
| Banco gerenciado | Preparado em codigo | RDS privado, TLS, segredos e bootstrap; falta executar na nuvem |
| Kubernetes com escalabilidade | Preparado em codigo | EKS, metrics-server, HPA, requests/limits e manifests; falta validar escala na nuvem |
| Terraform de provisionamento | Implementado/testado sem AWS | Roots de plataforma, banco, Lambda, backend/Gateway e API; aplicar e integrar ainda pendente |
| API executando em Kubernetes na nuvem | Pendente | Imagem/Job/Deployment prontos, testes locais e Kind; EKS nao demonstrado |
| Notificacoes serverless | Parcial, integracao pendente | Projeto e contrato de notificacoes existentes; envio real, eventos, retries e evidencia ponta a ponta ausentes |

O texto menciona notificacoes no desafio/objetivo. Elas devem permanecer no backlog
da entrega, mesmo sem o mesmo detalhamento dado a autenticacao nos requisitos.
Nao assumir que um webhook na API comprova notificacao serverless funcionando.

Alta disponibilidade aparece no objetivo do desafio. A decisao atual do RDS usa
Single-AZ no laboratorio, com Multi-AZ configuravel; isso nao comprova failover de
banco. Documentar o limite e revisar a configuracao final junto com os testes de
escala/disponibilidade do Kubernetes.

## Monitoramento e observabilidade

| Requisito | Estado verificado | Falta |
| --- | --- | --- |
| Integracao Datadog/New Relic ou equivalente | Pendente | Selecionar e integrar ferramenta; nao ha Datadog instalado |
| Latencia das APIs | Parcial | Autenticador registra duracao; faltam metricas/APM de todas as APIs e visualizacao |
| CPU/memoria Kubernetes | Preparacao parcial | metrics-server/HPA definidos; falta coleta historica e dashboard na ferramenta |
| Healthchecks e uptime | Parcial | /health e /health/live na API; falta monitoramento externo/uptime e alertas |
| Alertas de falhas de OS | Pendente | Instrumentar erros, definir condicoes e demonstrar disparo/recuperacao |
| Logs JSON e correlacao | Parcial | Autenticador emite JSON/X-Correlation-ID; falta padronizacao e propagacao ponta a ponta |
| Traces em execucao | Pendente | Instrumentacao distribuida e backend para consulta |
| Dashboard volume diario de OS | Pendente | Captura/exportacao, agregacao diaria e painel |
| Dashboard tempo por status | Parcial somente nos dados | Historico temporal persiste; faltam calculo/exportacao, painel e amostras representativas |
| Dashboard erros de integracao | Pendente | Metricas, logs correlacionados e painel |

### Divergencia a resolver no fluxo de status

O enunciado exige tempo medio por Diagnostico, Execucao e Finalizacao. O dominio
possui estado/metodo de Diagnostico, mas o fluxo atual de criacao utilizado pelo
cliente retorna Aguardando Aprovacao, e a aprovacao inicia Execucao. A documentacao
existente reconhece que esse caminho nao produz periodo de Diagnostico.

Antes de demonstrar o painel, definir um fluxo funcional legitimo que produza os
periodos exigidos e a correspondencia entre Finalizacao e o estado Finalizada.
Nao alterar automaticamente a criacao da OS ja acordada nem fabricar duracoes.
Sem amostras deve aparecer como sem dados, nao como tempo zero.

## Documentacao e entrega

| Requisito | Estado verificado | Falta |
| --- | --- | --- |
| Diagrama de componentes nuvem/API/banco/monitoramento | Parcial | Diagramas locais/cloud existem; monitoramento aparece pendente e precisa refletir a solucao final |
| Sequencia autenticacao e abertura de OS | Existe | docs/autenticacao-cliente-jwt.md; atualizar com Gateway e caminho final implantado |
| RFCs de decisoes relevantes | Parcial | RFC de plataforma AWS e decisoes em ADRs; revisar cobertura formal de nuvem, banco e autenticacao |
| ADRs permanentes | Existem, revisar cobertura | Plataforma, Gateway, RDS, JWT, historico e segredos; explicitar HPA/comunicacao e manter coerencia |
| Justificativa de banco | Existe | PostgreSQL/RDS e modelo relacional documentados; validar consistencia das afirmacoes |
| ER e relacionamentos | Parcial | ER do historico no repo API, descricoes das tabelas/FKs no banco; consolidar diagrama completo e acessivel no repo do banco |
| Consistencia/performance do banco | Parcial | FKs, unicidade, historico e migrations; validacao de consultas/indices sob carga ainda pendente |
| README por repo: proposito e execucao | Existe, revisar | Atualizar instrucoes apos CD/deploy e remover afirmacoes obsoletas |
| Diagrama especifico por README | Parcial | Ha diagramas e links em documentos; revisar os quatro, especialmente serverless e banco |
| Swagger/Postman | Local/documentado | Swagger da API e autenticador, OpenAPI; incluir URLs e instrucoes finais quando disponiveis |
| Dockerfiles quando aplicavel | API possui | Lambda atual usa ZIP, nao exige Dockerfile so por existir um repositorio |
| Links de deploy ativo | Pendentes | Nenhuma URL cloud funcional comprovada |
| Video ate 15 minutos | Nao apresentado | Autenticacao, pipeline, deploy automatico, API protegida, dashboard ao vivo, logs e traces |
| PDF unico | Nao apresentado | Links dos quatro repos, video, documentacao e confirmacao de acesso do avaliador |
| soat-architecture em todos os repos | Nao verificado | Conferir convite/acesso nos quatro repositorios pelo GitHub |

Exemplos de documentacao desatualizada encontrados: o documento de modelagem da
API ainda diz que verificacao de cliente ativo no autenticador esta pendente;
o modelo relacional no repo de banco ainda pede separar migrations no startup,
embora essa separacao ja tenha sido implementada na API. Corrigir na revisao dos
respectivos documentos, sem confundir texto antigo com o estado atual do codigo.

## Proxima sequencia de trabalho

1. Enquanto o acesso cloud esta bloqueado, implementar a observabilidade na API:
   logs JSON, correlacao/traces, latencia/erros e metricas de OS. Definir a ferramenta
   e os paineis/alertas. Resolver o fluxo de Diagnostico sem dados artificiais.
2. Completar notificacoes serverless e seus testes de integracao; revisar diagramas
   e documentacao de acordo com o comportamento final.
3. Preparar e validar CD dos quatro repositorios, com identidades/permissoes e
   ambientes separados. Conferir protecoes de branch e acesso do avaliador.
4. Resolver acesso a nuvem, provisionar os recursos, preencher segredos, executar
   bootstrap/migrations/grants e validar os dois ambientes com deploy automatico.
5. Executar demonstracao completa, revisar o checklist, gravar ate 15 minutos e
   montar o PDF de entrega.

Os passos de codigo/documentacao podem avancar sem console AWS. A comprovacao de
deploy, IAM, rede, dashboards cloud e uptime depende dos ambientes reais.
O enunciado permite qualquer nuvem; AWS e a escolha atual do projeto, nao uma
obrigacao academica. Migrar de provedor exige adaptar a infraestrutura existente.

Nao ha base para afirmar percentual de conclusao nem que falta apenas gravar o
video. Os maiores blocos restantes sao CD cloud, observabilidade e integracao real.
