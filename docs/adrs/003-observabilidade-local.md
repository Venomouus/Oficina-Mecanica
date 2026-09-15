# ADR 003 — OpenTelemetry e Grafana para demonstracao local

Status: aceita para laboratorio. Data: 2026-09-14.

## Contexto

O Tech Challenge exige latencia, logs, traces, alertas e indicadores de OS. O acesso
AWS esta indisponivel e a entrega precisa de evidencias executaveis sem conta paga.

## Decisao

Instrumentar a API com OpenTelemetry e exportar OTLP para Grafana LGTM local
(Prometheus, Loki e Tempo). Provisionar dashboard e regras em arquivos versionados.
Consultar indicadores de negocio a cada 10 segundos no banco local, sem fabricar
duracoes. Usar trace_id nos logs e templates de rota para limitar cardinalidade.
Erros 5xx em OS disparam alerta; rejeicoes 4xx de autorizacao/validacao nao disparam.

O fluxo padrao de abertura continua Aguardando Aprovacao. A abertura administrativa
opcional em Diagnostico permite registrar esse periodo real antes do orcamento.
Finalizacao corresponde a permanencia em Finalizada ate Entregue. Metricas de CPU
e memoria locais sao do processo, claramente distintas de metricas do Kubernetes.

## Alternativas e consequencias

Datadog/New Relic tambem atendem ao tipo de ferramenta solicitado, mas exigiriam
configuracao externa. O formato OTLP permite trocar o destino futuramente.
LGTM all-in-one simplifica a demonstracao, consome recursos locais e nao e uma
topologia de producao/alta disponibilidade. Nao resolve CD cloud, monitoramento
Kubernetes, notificacoes serverless nem uptime medido por sonda externa.
Alertas ficam na interface Grafana; entrega externa e escalonamento sao pendentes.

Referencia: https://github.com/grafana/docker-otel-lgtm/tree/v0.33.0
