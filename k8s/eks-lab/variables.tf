variable "aws_region" {
  description = "Regiao AWS do laboratorio."
  type        = string
  default     = "sa-east-1"
}

variable "cluster_name" {
  description = "Nome exclusivo do cluster de laboratorio."
  type        = string
  default     = "oficina-fase4-lab"
}

variable "node_instance_type" {
  description = "Um unico no de laboratorio; 8 GiB para servicos, bancos e RabbitMQ."
  type        = string
  default     = "m7i-flex.large"
}

variable "api_public_access_cidrs" {
  description = "CIDRs que podem alcancar a API do EKS. O runner GitHub hospedado tem IP dinamico; restringir quando possivel."
  type        = list(string)
  default     = ["0.0.0.0/0"]
}
