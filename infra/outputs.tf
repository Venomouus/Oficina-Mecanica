output "cluster_name" {
  description = "Nome do cluster Kubernetes criado pelo Terraform."
  value       = var.cluster_name
}

output "kubernetes_context" {
  description = "Contexto kubectl do cluster kind."
  value       = "kind-${var.cluster_name}"
}

output "namespace" {
  description = "Namespace da aplicacao no Kubernetes."
  value       = "oficina"
}

output "health_check_url" {
  description = "URL local esperada para validar a API quando o port mapping do kind estiver ativo."
  value       = "http://localhost:30080/health"
}

output "swagger_url" {
  description = "URL local do Swagger quando ASPNETCORE_ENVIRONMENT estiver como Development."
  value       = "http://localhost:30080/swagger"
}

output "port_forward_command" {
  description = "Comando alternativo caso a porta 30080 nao responda no ambiente local."
  value       = "kubectl port-forward service/oficina-api 30081:8080 -n oficina"
}