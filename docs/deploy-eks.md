# API no EKS e migrations separadas

Esta entrega prepara codigo e testes locais. Nenhum recurso AWS foi provisionado,
nenhum segredo foi preenchido e nenhum deploy foi executado. `DEPLOY_ENABLED`
continua `false`. A CI nova valida Terraform com mocks e executa a imagem contra
PostgreSQL descartavel; nao possui credenciais AWS nem permissao OIDC de deploy.

## Componentes e responsabilidade

| Local | Responsabilidade |
| --- | --- |
| `aws/` desta API | ECR imutavel por ambiente, duas roles IRSA, container do segredo administrativo |
| `deploy/render.py` | Manifests JSON aceitos pelo kubectl, gerados de contratos publicos reais |
| `Oficina.API --migrate` | Processo sem servidor HTTP; executa migrations como usuario migrations e termina |
| `Oficina-infra-database/bootstrap` | Prepara bancos/usuarios, depois aplica grants nas tabelas migradas |
| `Oficina-infra-kubernetes/backend` | ALB privado, target group, VPC Link, Service ClusterIP e TargetGroupBinding |
| `Oficina-infra-kubernetes/infra` | VPC, EKS, namespaces/operacao do cluster e identidade do Load Balancer Controller |

Os manifests desta API **nao criam Service, Ingress, TargetGroupBinding ou namespace**.
O administrador cria os namespaces `oficina-staging` e `oficina-producao` e instala
o controller e os manifests do backend antes de publicar a aplicacao. O contrato
preservado e `app=oficina-api`, porta 8080 e health check do ALB `/health`.

`infra/` e `k8s/` continuam sendo o laboratorio local com Kind. O novo root `aws/`
tem state S3 proprio por ambiente e nao reutiliza o state local existente.

## Configuracao e segredos

O output `api` de `aws/` fornece apenas ARNs, nomes e endereco de banco. Os pods
leem Secrets Manager uma vez na inicializacao usando IRSA (credenciais temporarias
do SDK, pacote STS incluido); nao ha chaves AWS ou Kubernetes Secrets nos manifests.
Nao montar credenciais do usuario nem habilitar fallback para a role EC2 do node.

| Processo | Service account | Leitura no Secrets Manager | PostgreSQL |
| --- | --- | --- | --- |
| API | `oficina-api` | `database/app` e `api/config` do ambiente | `oficina_<ambiente>_app` |
| Job | `oficina-migrations` | somente `database/migrations` do ambiente | `oficina_<ambiente>_migrations` |

Os segredos de banco pertencem ao infra-database, com JSON `username` e `password`.
O novo segredo `oficina/<ambiente>/api/config` recebe JSON com os campos:
`jwtKey` (aleatorio, pelo menos 32 bytes), `adminUser`, `adminPassword` (aleatoria,
pelo menos 16 caracteres) e `budgetToken` (aleatorio, pelo menos 32 caracteres).
Preencher os valores pela sessao administrativa controlada de bootstrap/Secrets
Manager, nunca pelo Terraform, argumento de comando, commit ou log de CI.

O endereco do RDS vem do contrato publico, nao do JSON secreto. O processo verifica
o nome exato do usuario de acordo com sua finalidade e usa TLS `VerifyFull` com
o bundle publico em `Oficina.API/certificates/rds-global-bundle.pem`. O pool da API
tem limite 10 por pod; quatro replicas mais uma de rollout podem usar ate 50
conexoes, alem de Lambda, Job e administracao. Ajustar limites apos medir no RDS.

Rotacao exige atualizar o banco e o valor do segredo de forma coordenada e iniciar
novos pods. A anotacao publica `release` provoca rollout quando alterada. Segredos
nao sao recarregados continuamente. A rotacao da chave administrativa invalida
tokens existentes; planejar a janela de troca.

## Validacao local desta entrega

```powershell
dotnet test Oficina.Tests/Oficina.Tests.csproj --configuration Release
terraform -chdir=aws init -backend=false
terraform -chdir=aws validate
terraform -chdir=aws test
python -m unittest discover -s deploy/tests -v
python deploy/render.py --config deploy/config.example.json
docker build -t oficina-api:eks-test .
python deploy/smoke.py --bootstrap-path ../Oficina-Database/Oficina-infra-database/bootstrap
```

O exemplo contem identificadores ficticios e **nao deve ser aplicado**. O teste
Docker usa o bootstrap real, cria um PostgreSQL isolado sem publicar portas,
executa migrations duas vezes, aplica grants e inicia a API com usuario restrito.
Valida uid 1654, filesystem readonly e readiness sem permissao para ler
`__EFMigrationsHistory`. Remove somente seus containers/volumes identificados por
label. Usa TLS desabilitado apenas nesse teste local; TLS/RDS e IRSA ainda exigem
verificacao real na AWS. A CI fixa o bootstrap no commit
`27c1bdf3852c40361b3971a92746079b42478820` do repositorio de banco.

Em `Development`, `Database:MigrateOnStartup=true` preserva Docker Compose e o
laboratorio Kind. Em `Production`, essa opcao causa falha de inicializacao: o
servidor HTTP nunca executa migrations. Para executar explicitamente no ambiente
local, configurar a connection string do usuario de migrations e usar:

```powershell
dotnet run --project Oficina.API --no-launch-profile -- --migrate
```

## Ordem da primeira implantacao AWS

Esta secao e um roteiro para a etapa AWS; nao e executada pela CI desta entrega.

1. Preparar conta, identidade de operacao e state remoto com locking. Aplicar
   fundacao VPC/EKS, RDS e os containers de segredos, seguindo seus runbooks.
   O endpoint Kubernetes e privado: usar uma sessao/runner com acesso a VPC.
2. Copiar `aws/terraform.tfvars.json.example` para `aws/terraform.tfvars.json` e preencher `environment`, `aws_account_id`,
   `aws_region`, `platform`, `database` e `runtime_secret_arns`, copiando os tres
   outputs publicos reais dos repositorios de infra. Criar backend por ambiente
   a partir de `aws/backend.hcl.example`; revisar o plan antes do apply.
   Nao copiar o `bootstrap_secret_arn` do master para estas variaveis.
3. Publicar a imagem revisada no ECR criado por este root. A sessao de publicacao
   precisa de permissoes ECR, separadas das roles dos pods. Registrar o digest
   retornado pelo ECR e usa-lo tanto no Job quanto no Deployment. O fluxo atual
   GHCR continua separado; esta entrega nao ativa publicacao ECR automatica.
4. Executar a fase `prepare` do bootstrap de banco. Preencher os segredos app,
   migrations e auth com as credenciais correspondentes e o segredo `api/config`.
   Os logins app/auth permanecem bloqueados ate concluir migrations e grants.
5. Instalar o Load Balancer Controller, namespaces e Service/TargetGroupBinding
   do root backend. Criar o Gateway inicialmente com rotas publicas de autenticacao,
   configurar Lambda com o issuer e verificar discovery/JWKS HTTPS antes de habilitar
   o authorizer JWT. Seguir o runbook gateway para resolver essa dependencia.
6. Copiar `deploy/config.example.json` para `deploy/config.local.json`; substituir
   `api`, `gateway`, `backend` pelos outputs reais, `image_digest`, `release` e os
   CIDRs reais das subnets privadas do ALB e do banco. Gerar os manifests e revisar.
7. Aplicar `setup.json` (SAs e NetworkPolicies), depois `migration.json`. Aguardar
   sucesso do Job antes de continuar. Ele tem prazo de 600 segundos, sem repeticao
   automatica, e lock de sessao PostgreSQL para impedir migrations concorrentes.
8. Executar `grants` do bootstrap: conferir propriedade das tabelas, liberar CRUD
   para app e somente consulta autorizada para auth. So entao aplicar `api.json`.
9. Aguardar Deployment, HPA e targets saudaveis; habilitar a integracao privada
   no Gateway e repetir os testes de CPF/JWT, criacao/aprovacao da OS e outro cliente
   recebendo 404. Confirmar ausencia de acesso publico direto ao backend.

Exemplo de comandos **somente depois de revisar os contratos reais**:

```powershell
python deploy/render.py --config deploy/config.local.json
kubectl --context <contexto-revisado> apply -f deploy/generated/setup.json
kubectl --context <contexto-revisado> apply -f deploy/generated/migration.json
kubectl --context <contexto-revisado> wait --for=condition=complete job/oficina-migrate-<release> -n oficina-staging --timeout=610s
# Executar grants conforme o runbook infra-database antes da API.
kubectl --context <contexto-revisado> apply -f deploy/generated/api.json
kubectl --context <contexto-revisado> rollout status deployment/oficina-api -n oficina-staging --timeout=300s
```

Manter deployments serializados por ambiente. Um Job com falha bloqueia a promocao;
inspecionar seu status, corrigir a causa e criar nova `release` apos revisao. Nao
aplicar `api.json` incondicionalmente em um script que ignore exit codes.

## Rede, saude e operacao

NetworkPolicy permite entrada na API somente dos CIDRs privados do ALB em 8080;
Job nao recebe entrada. Saida permite DNS para CoreDNS, PostgreSQL nas subnets do
banco e HTTPS para Secrets Manager/STS/discovery/JWKS. NetworkPolicy nao filtra por
hostname: HTTPS fica permitido em qualquer destino, exceto metadata/link-local e
loopback. Depende do VPC CNI com NetworkPolicy habilitado. Confirmar CIDRs reais,
SGs e caminho de NAT/endpoints; nao assumir que um teste offline valida a rede AWS.

`/health/live` verifica o processo; `/health` consulta `Clientes.Id/Ativo` com o
usuario app e retorna 503 em falha. Nao consulta o historico EF nem garante que
todas as operacoes de negocio funcionem: executar smoke funcional apos o rollout.
Startup e liveness usam `/health/live`, prontidao e ALB usam `/health`.

O HPA controla 2 a 4 replicas; o Deployment omite `replicas` para nao sobrescrever
o HPA a cada apply. PDB mantem uma replica em evacuacoes voluntarias. A imagem roda
como uid 1654 sem capabilities, filesystem readonly e apenas `/tmp` gravavel.
Logs seguem stdout/stderr; coleta centralizada, dashboards e alarmes ainda dependem
da etapa de observabilidade. Migrations reportam erro generico/SQLSTATE sem SQL,
connection string ou exception interna com dados sensiveis.

Rollback da aplicacao usa digest anterior previamente revisado, desde que compativel
com o schema atual. Nao desfazer migrations automaticamente. Alteracoes destrutivas
exigem plano de backup/restauracao e compatibilidade entre versoes.

Referencias: [IRSA e SDK](https://docs.aws.amazon.com/eks/latest/userguide/iam-roles-for-service-accounts.html),
[probes Kubernetes](https://kubernetes.io/docs/concepts/workloads/pods/probes/),
[usuario nao root do .NET 8](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8/containers),
[bundle CA publico RDS](https://truststore.pki.rds.amazonaws.com/global/global-bundle.pem).
