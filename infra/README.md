# Infraestrutura como Codigo - Terraform

Esta pasta provisiona um cluster Kubernetes local com `kind` para executar a API da oficina mecanica e o PostgreSQL usando os manifestos da pasta `../k8s`.

## Recursos criados

- Cluster Kubernetes local `oficina-local` com kind.
- Mapeamento da porta local `30080` para o NodePort da API.
- Build da imagem Docker `oficina-api:k8s-health`.
- Carregamento da imagem local para dentro do cluster kind.
- Aplicacao dos manifestos Kubernetes:
  - Namespace.
  - ConfigMap.
  - Secret de desenvolvimento.
  - Deployment e Service do PostgreSQL.
  - Deployment e Service da API.
  - HPA da API.

## Pre-requisitos

Instale e deixe disponivel no PATH:

- Docker Desktop.
- Terraform.
- kind.
- kubectl.

Valide com:

```powershell
docker version
terraform version
kind version
kubectl version --client
```

## Como aplicar

A partir da raiz do repositorio:

```powershell
cd infra
terraform init
terraform plan
terraform apply
```

Confirme com `yes` quando o Terraform pedir.

Depois valide os recursos:

```powershell
kubectl config use-context kind-oficina-local
kubectl get pods -n oficina
kubectl get svc -n oficina
kubectl get hpa -n oficina
```

A API deve ficar com dois pods `1/1 Running` e o PostgreSQL com um pod `1/1 Running`.

## Como testar a API

Primeiro tente o endpoint exposto pelo NodePort:

```powershell
curl http://localhost:30080/health
```

Se o ambiente local nao encaminhar o NodePort corretamente, use port-forward:

```powershell
kubectl port-forward service/oficina-api 30081:8080 -n oficina
```

Em outro terminal:

```powershell
curl http://localhost:30081/health
```

Com o ambiente `Development` no ConfigMap, o Swagger fica em:

```text
http://localhost:30080/swagger
```

ou, via port-forward:

```text
http://localhost:30081/swagger
```

## Como destruir

```powershell
terraform destroy
```

Isso remove o cluster kind e, junto com ele, os recursos Kubernetes criados para a aplicacao.

## Observacao sobre Secrets

Os valores presentes em `../k8s/secret.yaml` sao apenas para desenvolvimento local e demonstracao academica. Em producao, estes valores devem ser substituidos por secrets reais do ambiente, GitHub Secrets ou um gerenciador de segredos da cloud.

## Observacao sobre HPA

O manifesto do HPA esta criado e aplicado. Em clusters locais, CPU/memoria podem aparecer como `<unknown>` quando nao houver metrics-server instalado. Em um cluster com metrics-server, o HPA usa os limites configurados para escalar a API automaticamente.