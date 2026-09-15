variable "project_name" {
  type    = string
  default = "oficina"
  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{1,19}$", var.project_name))
    error_message = "Prefixo de 2 a 20 caracteres minusculos."
  }
}
variable "environment" {
  type = string
  validation {
    condition     = contains(["staging", "producao"], var.environment)
    error_message = "Use staging ou producao."
  }
}
variable "aws_account_id" {
  type = string
  validation {
    condition     = can(regex("^[0-9]{12}$", var.aws_account_id))
    error_message = "Conta AWS deve ter 12 digitos."
  }
}
variable "aws_region" {
  type    = string
  default = "us-east-1"
}
variable "platform" {
  type = object({
    contract_version  = number
    aws_region        = string
    vpc_id            = string
    cluster_name      = string
    oidc_provider_arn = string
    oidc_issuer_url   = string
    environments      = map(object({ namespace = string, branch = string }))
  })
  validation {
    condition = (
      var.platform.contract_version == 1 && var.platform.aws_region == var.aws_region &&
      (var.academy_role_arn != null ? true : startswith(var.platform.oidc_provider_arn, "arn:aws:iam::${var.aws_account_id}:oidc-provider/")) &&
      try(var.platform.environments[var.environment].namespace == "oficina-${var.environment}", false) &&
      try(var.platform.environments[var.environment].branch == (var.environment == "staging" ? "develop" : "master"), false)
    )
    error_message = "Contrato platform v1 deve corresponder a conta, regiao, namespace e branch do ambiente."
  }
}
variable "database" {
  type = object({
    contract_version     = number
    aws_region           = string
    vpc_id               = string
    address              = string
    port                 = number
    ssl_mode             = string
    planned_environments = map(object({ database_name = string, app_role = string, migrations_role = string }))
  })
  validation {
    condition = (
      var.database.contract_version == 1 && var.database.aws_region == var.aws_region &&
      var.database.vpc_id == var.platform.vpc_id && var.database.port == 5432 && var.database.ssl_mode == "VerifyFull" &&
      can(regex("^[a-zA-Z0-9.-]+\\.rds\\.amazonaws\\.com$", var.database.address)) &&
      try(var.database.planned_environments[var.environment].database_name == "oficina_${var.environment}", false) &&
      try(var.database.planned_environments[var.environment].app_role == "oficina_${var.environment}_app", false) &&
      try(var.database.planned_environments[var.environment].migrations_role == "oficina_${var.environment}_migrations", false)
    )
    error_message = "Contrato database v1 deve corresponder a VPC/regiao, TLS e usuarios separados do ambiente."
  }
}
variable "runtime_secret_arns" {
  description = "Output runtime_secret_arns do infra-database; nunca ARN do master."
  type        = map(object({ app = string, migrations = string }))
  validation {
    condition = try(alltrue([for purpose in ["app", "migrations"] : can(regex(
      "^arn:aws:secretsmanager:${var.aws_region}:${var.aws_account_id}:secret:${var.project_name}/${var.environment}/database/${purpose}-[A-Za-z0-9]{6}$",
      var.runtime_secret_arns[var.environment][purpose]
    ))]), false)
    error_message = "ARNs devem ser dos segredos app/migrations deste ambiente, conta e regiao."
  }
}
