# EKS economico para a Fase 4 (laboratorio temporario)

Esta pasta cria **um** cluster EKS com um node group On-Demand de **um** no,
VPC, duas sub-redes publicas, Internet Gateway e tres repositorios ECR. Nao cria NAT Gateway, RDS,
DocumentDB ou Load Balancer. Os tres microsservicos, PostgreSQL, dois bancos
MongoDB e RabbitMQ sao implantados separadamente no namespace `oficina`.
O no unico nao oferece alta disponibilidade e nao representa producao.
O tipo `m7i-flex.large` (8 GiB) aparece como elegivel ao plano gratuito
nesta conta/regiao; a elegibilidade nao significa custo zero para todo o
laboratorio. Confirme novamente antes de recriar o node group.

O ambiente foi validado com um fluxo completo da Saga no EKS: OS criada,
orcamento gerado, pagamento local simulado e OS `FINALIZADA`. Isso nao testa
o Mercado Pago externo. `infra-lab.yaml` usa `emptyDir`: qualquer recriacao
dos pods de banco ou RabbitMQ perde os dados. Nao use o lab para dados reais.

Para recriar a infraestrutura interna e as credenciais aleatorias sem grava-las
em Git, use `./k8s/eks-lab/setup-lab.ps1`. O script preserva as senhas ja
existentes e reaplica os Secrets de conexao. Depois aplique
`kubectl apply -f k8s/eks-lab/deploy-rbac.yaml`. O teste fim a fim pode ser
repetido com `./k8s/eks-lab/smoke-lab.ps1`, com o perfil AWS `default` ativo.

O Terraform tambem cria tres repositorios ECR e uma role OIDC restrita aos
tres repositorios GitHub da oficina na branch `main`. Os workflows dos servicos
publicam imagens no ECR e atualizam o EKS sem secrets de kubeconfig ou AWS.
O grupo Kubernetes de deploy so tem permissoes para Deployments, Services e
leitura de Pods/ReplicaSets no namespace `oficina`.

Para a observabilidade de demonstracao, aplique
`kubectl apply -f k8s/eks-lab/observability-lab.yaml` e reinicie os tres
deployments de aplicacao para carregar `observability-config`. O LGTM usa o
mesmo pacote fixado no Compose local e recebe OTLP dos tres servicos. O painel
nao e publico: use `kubectl -n oficina port-forward svc/otel-lgtm 3000:3000`
e abra `http://localhost:3000` (usuario `admin`; a senha aleatoria esta no
Secret `grafana-credentials` do namespace). Metricas
`oficina_saga_events_published_total` ja foram observadas com `service_name`
para os tres servicos, assim como logs e spans exportados. Os dados de
observabilidade tambem usam `emptyDir` e se perdem ao recriar o pod.

O EKS e cobrado desde a criacao ate a exclusao do cluster, mesmo sem pods.
Tambem ha custos de EC2, disco EBS, IPv4 publico e transferencia. Nao execute
`apply` sem conferir o plano e o orcamento da conta. O estado Terraform local
e necessario para `destroy`; guarde-o fora do Git. Nao grave credenciais em
arquivos versionados. Os CIDRs padrao deixam a API Kubernetes alcancavel pela
internet, com autenticacao exigida; restrinja-os quando possivel.

Para esta conta da nova experiencia AWS, o usuario aprovou uma excecao de
laboratorio de dois dias: usar o `aws login` temporario do perfil `default`,
que foi identificado como root. Nao crie access keys root nem copie credenciais
para o GitHub. Em ambiente permanente, use uma identidade nao-root. Apague os
recursos ao terminar a demonstracao.

## Preflight

```powershell
aws sts get-caller-identity --profile default --region sa-east-1
aws eks list-clusters --profile default --region sa-east-1
$env:AWS_PROFILE = 'default'
terraform -chdir=k8s/eks-lab init
terraform -chdir=k8s/eks-lab plan '-out=lab.tfplan'
```

Depois de revisar **todos** os recursos e custos previstos, para criar:

```powershell
terraform -chdir=k8s/eks-lab apply lab.tfplan
aws eks update-kubeconfig --name oficina-fase4-lab --region sa-east-1 --profile default
kubectl get nodes
```

Ao terminar a demonstracao, antes de completar dois dias:

```powershell
$env:AWS_PROFILE = 'default'
terraform -chdir=k8s/eks-lab destroy
```

`terraform destroy` nao remove recursos criados separadamente fora deste
modulo, como balanceadores, volumes persistentes e Secrets. Confira o console
AWS apos a limpeza. Um alerta de orcamento nao desliga recursos automaticamente.

Nao execute os workflows de deploy apos o `destroy` sem primeiro recriar o
ambiente. Atualize a documentacao e os workflows se IDs dos repositorios,
conta AWS ou nome do cluster mudarem.
