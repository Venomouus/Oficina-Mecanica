# Fase 4: deploy e observabilidade

Cada repositorio de servico executa build, testes com cobertura minima de 80%,
SonarCloud com espera pelo Quality Gate e build Docker no PR. Um push em `main`
so inicia `publish-deploy` apos as verificacoes passarem. Esse job publica no
GHCR as tags do SHA e `latest`, aplica o manifesto do proprio servico, fixa a
imagem pelo SHA e espera o rollout Kubernetes. O OS Service tambem exige o BDD
distribuido antes de publicar.

## Preparar o cluster (uma vez)

1. Use um cluster cuja API seja acessivel a partir de um runner GitHub Actions
   (ou use runner self-hosted na rede do cluster). O Kubernetes local do Docker
   Desktop nao e acessivel de um runner hospedado pelo GitHub.
2. Crie o namespace `oficina` e infra de PostgreSQL, MongoDB e RabbitMQ com
   armazenamento persistente e credenciais proprias do ambiente. O arquivo
   `infra.yaml` desta pasta e somente demonstrativo: usa credenciais de exemplo
   e bancos sem volumes persistentes. Nao o aplique em producao.
3. Provisione os Secrets `messaging-secrets` (`username`, `password`),
   `os-service-secrets`, `billing-service-secrets` e
   `execution-service-secrets` (cada um com `database-connection`), alem de
   `mercado-pago-secrets`
   (`access-token`, `external-pos-id`, `webhook-url`, `webhook-secret`) com um
   gerenciador de segredos do cluster. Nao grave valores reais no Git.
4. Em **cada** repositorio GitHub, configure o secret Actions `KUBE_CONFIG_B64`
   com o kubeconfig codificado em Base64. A identidade Kubernetes deve ter
   somente permissoes para ler os Secrets exigidos, aplicar Deployment/Service
   e acompanhar rollouts no namespace `oficina`. Configure as imagens GHCR
   como publicas ou um `imagePullSecret` na service account do namespace.
5. Configure protecao de `main`: PR obrigatorio e checks
   `build-test-analyze`, SonarCloud e, para OS, `distributed-bdd` obrigatorios.
   Essa configuracao de GitHub e externa ao codigo.

Sem `KUBE_CONFIG_B64` e esses recursos, o job falha explicitamente. A presenca
do workflow nao comprova deploy real: confirme o job `publish-deploy`, as
imagens SHA e `kubectl -n oficina get deployments` apos o merge.

## Telemetria

Os tres servicos exportam logs, traces HTTP e metricas HTTP/Saga via OTLP gRPC
somente quando `OTEL_EXPORTER_OTLP_ENDPOINT` estiver definido. Logs continuam
em JSON no stdout mesmo sem coletor. Os contadores
`oficina.saga.events.published`, `consumed` e `failed` possuem a tag
`event.type`; spans `saga.publish` e `saga.consume` mostram a operacao RabbitMQ.
O nome do servico no recurso OTLP e `os-service`, `billing-service` ou
`execution-service`. Nao use IDs de OS, dados pessoais ou tokens como tags de
metricas.

Em Kubernetes, crie opcionalmente o ConfigMap `observability-config` com a
chave `otlp-endpoint`, por exemplo `http://otel-collector.observability:4317`.
O coletor deve encaminhar metricas para Prometheus/Mimir, logs para Loki e
traces para Tempo; pode ser a infraestrutura ja usada na Fase 3. O ConfigMap
nao e criado pelos repositorios de servico porque seu endereco depende do
cluster.

No Docker Desktop, o LGTM de `local/compose.yml` pode receber OTLP na porta
14317. Inicie-o e defina `OTEL_EXPORTER_OTLP_ENDPOINT` para
`http://host.docker.internal:14317` antes de iniciar o Compose dos servicos.
Essa porta de ingestao e para desenvolvimento local; nao a exponha em uma rede
publica. Confira os tres `service.name` no Grafana, gere uma OS de teste e
observe requisicoes HTTP e eventos da Saga. O fluxo Mercado Pago real continua
exigindo credenciais e webhook validos.
