# ADR 004 — Comunicacao HTTP e escalabilidade da API

Status: aceita no codigo; Data: 2026-09-14.

## Decisao

Manter API REST/JSON sincrona para comandos e consultas de OS e autenticacao CPF.
O cliente apresenta JWT; a API valida assinatura e identidade, e aplica autorizacao
por proprietario. O JWT administrativo nao substitui o JWT do cliente. O Gateway
preparado no Terraform adiciona roteamento e validacao na borda, sem retirar a
autorizacao de negocio da API. Notificacoes assincronas permanecem pendentes.

Para o laboratorio Kubernetes, `k8s/hpa.yaml` define autoscaling/v2 com 2 a 5 replicas,
CPU alvo de 70% e memoria de 80%, usando metrics-server e requests de recursos.
A aplicacao nao guarda sessao HTTP em memoria compartilhada; estado de negocio fica
no PostgreSQL. Migracoes devem ser coordenadas separadamente no deploy EKS.

## Consequencias

HPA depende de metricas disponiveis e capacidade dos nodes; declaracao YAML nao prova
escalabilidade. O Compose da demonstracao executa uma API. Escala, failover,
balanceamento e limites de conexao com banco precisam de validacao no cluster.
Chamadas sincronas simplificam a demonstracao, mas exigem limites de tempo e tratamento
de indisponibilidade. O historico e a concorrencia da OS sao controlados no dominio.
