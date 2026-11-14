# Kubernetes local

Os manifests foram separados em infraestrutura e serviços. A infraestrutura
cria o namespace `oficina`, PostgreSQL do OS Service, MongoDB compartilhado em
instâncias lógicas separadas e RabbitMQ. Cada serviço mantém seu próprio
Deployment e Service no respectivo repositório.

## Pré-requisitos

- Docker Desktop com Kubernetes habilitado ou Minikube.
- Imagens locais construídas no mesmo runtime do cluster.

## Execução com Docker Desktop Kubernetes

Na raiz do projeto:

```powershell
docker build -t oficina-os-service:local ./services/os-service
docker build -t oficina-billing-service:local ./services/billing-service
docker build -t oficina-execution-service:local ./services/execution-service
kubectl apply -f k8s/infra.yaml
kubectl apply -f services/os-service/k8s/deployment.yaml
kubectl apply -f services/billing-service/k8s/deployment.yaml
kubectl apply -f services/execution-service/k8s/deployment.yaml
kubectl get pods -n oficina
```

Para testar as APIs localmente:

```powershell
kubectl port-forward -n oficina service/os-service 8081:8081
kubectl port-forward -n oficina service/billing-service 8082:8082
kubectl port-forward -n oficina service/execution-service 8083:8083
```

Os Swagger ficam disponíveis nas portas `8081`, `8082` e `8083`.
