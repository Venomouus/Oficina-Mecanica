# RFC — Entrega demonstravel sem acesso a nuvem

Estado: proposta adotada para a entrega parcial. Data: 2026-09-14.

## Problema

O acesso ao laboratorio AWS nao foi liberado a tempo. O enunciado exige evidencias
de autenticacao, observabilidade, infraestrutura e documentacao. Codigo sem execucao
nao comprova deploy, mas a parte aplicacional pode ser demonstrada localmente.

## Proposta e escolhas

Preservar AWS como arquitetura alvo ja preparada em Terraform (EKS, RDS, Lambda e
API Gateway). Usar Docker Compose somente como ambiente de demonstracao. Nao trocar
de provedor nesta etapa, pois isso exigiria revalidar IAM, rede, deploy e custos.

Manter PostgreSQL pela integridade relacional e transacional de clientes, OS, itens
e historico. Manter autenticacao CPF com consulta de existencia/status e JWT RS256,
compartilhando apenas a chave publica por JWKS. Usar Grafana LGTM para mostrar
metricas, logs e traces sem credenciais de um servico externo.

## Criterios de evidencia

O avaliador deve conseguir executar o Compose, autenticar cliente, criar/aprovar OS,
ver isolamento de acesso e abrir dashboard/logs/traces. Diagramas e PDF devem apontar
explicitamente o que e local e o que ainda nao foi implantado. A aprovacao de CI nao
sera descrita como CD cloud. Video e permissao do avaliador devem ser reais.

## Riscos e retorno a arquitetura alvo

Esta proposta nao atende integralmente aos requisitos cloud, Kubernetes em operacao,
notificacoes serverless e deploy automatico hom/prod. Quando houver acesso, revisar
permissoes do laboratorio, configurar ambientes/segredos, aplicar Terraform e concluir
as integracoes. Reutilizar instrumentacao OTLP; dimensionar o backend de observabilidade
para producao. A topologia LGTM all-in-one fica restrita ao desenvolvimento.
