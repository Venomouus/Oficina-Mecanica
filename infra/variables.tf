variable "cluster_name" {
  description = "Nome do cluster Kubernetes local criado pelo kind."
  type        = string
  default     = "oficina-local"
}

variable "app_image" {
  description = "Imagem Docker local usada pelo Deployment da API. Deve bater com k8s/api-deployment.yaml."
  type        = string
  default     = "oficina-api:k8s-health"
}

variable "apply_manifests" {
  description = "Quando true, o Terraform tambem faz build da imagem, carrega no kind e aplica os manifests de ../k8s."
  type        = bool
  default     = true
}

variable "command_interpreter" {
  description = "Interpretador usado pelos comandos locais do Terraform. O padrao atende Windows PowerShell."
  type        = list(string)
  default     = ["PowerShell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]
}