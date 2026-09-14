# Recursos AWS da API

Root independente do laboratorio Kind em `../infra/`. Cria ECR, roles IRSA e
container do segredo administrativo por ambiente. Nao cria rede, banco, cluster,
Service, ALB, Gateway, valor secreto ou identidade de publicacao/deploy.

Copiar `terraform.tfvars.json.example` para `terraform.tfvars.json` e preencher os
outputs publicos reais da plataforma e do banco. O exemplo tem IDs ficticios.
Usar backend S3 proprio por ambiente conforme `backend.hcl.example`.

Validacao sem AWS: `terraform init -backend=false`, `terraform validate`,
`terraform test` (provider simulado). Nao rodar apply antes da etapa AWS revisada.

Ver [runbook completo](../docs/deploy-eks.md) para segredos, primeira implantacao,
bootstrap/migrations/grants, isolamento e validacoes ainda pendentes na nuvem.
