data "aws_eks_cluster" "selected" { name = var.platform.cluster_name }
data "aws_iam_openid_connect_provider" "selected" {
  count = var.academy_role_arn == null ? 1 : 0
  arn   = var.platform.oidc_provider_arn
}

locals {
  namespace        = var.platform.environments[var.environment].namespace
  oidc_host        = trimprefix(var.platform.oidc_issuer_url, "https://")
  service_accounts = { app = "oficina-api", migrations = "oficina-migrations" }
}

resource "aws_ecr_repository" "api" {
  name                 = "${var.project_name}/${var.environment}/api"
  image_tag_mutability = "IMMUTABLE"
  force_delete         = false
  image_scanning_configuration { scan_on_push = true }
  encryption_configuration { encryption_type = "AES256" }
  lifecycle {
    precondition {
      condition = (
        data.aws_eks_cluster.selected.vpc_config[0].vpc_id == var.platform.vpc_id &&
        data.aws_eks_cluster.selected.identity[0].oidc[0].issuer == var.platform.oidc_issuer_url &&
        (var.academy_role_arn != null ? true : (
          trimprefix(data.aws_iam_openid_connect_provider.selected[0].url, "https://") == local.oidc_host &&
          contains(data.aws_iam_openid_connect_provider.selected[0].client_id_list, "sts.amazonaws.com")
        ))
      )
      error_message = "Cluster e provedor OIDC reais devem corresponder ao contrato platform."
    }
  }
}

# Container only: no values in Terraform/state. Fill via the controlled bootstrap session.
resource "aws_secretsmanager_secret" "api" {
  name                    = "${var.project_name}/${var.environment}/api/config"
  description             = "JWT administrativo, login e token de integracao da API."
  recovery_window_in_days = 30
}

resource "aws_iam_role" "workload" {
  for_each = var.academy_role_arn != null ? {} : local.service_accounts
  name     = "${var.project_name}-${var.environment}-${each.value}"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Action    = "sts:AssumeRoleWithWebIdentity"
      Principal = { Federated = var.platform.oidc_provider_arn }
      Condition = { StringEquals = {
        "${local.oidc_host}:sub" = "system:serviceaccount:${local.namespace}:${each.value}"
        "${local.oidc_host}:aud" = "sts.amazonaws.com"
      } }
    }]
  })
}
resource "aws_iam_role_policy" "secrets" {
  for_each = var.academy_role_arn != null ? {} : local.service_accounts
  name     = "read-own-secrets"
  role     = aws_iam_role.workload[each.key].id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = ["secretsmanager:GetSecretValue"]
      Resource = concat([var.runtime_secret_arns[var.environment][each.key]],
      each.key == "app" ? [aws_secretsmanager_secret.api.arn] : [])
    }]
  })
}

output "api" {
  value = {
    contract_version = 1
    environment      = var.environment
    aws_region       = var.aws_region
    aws_account_id   = var.aws_account_id
    cluster_name     = var.platform.cluster_name
    namespace        = local.namespace
    image_repository = aws_ecr_repository.api.repository_url
    database_host    = var.database.address
    database_name    = var.database.planned_environments[var.environment].database_name
    academy_mode     = var.academy_role_arn != null
    role_arns        = var.academy_role_arn != null ? { for purpose in keys(local.service_accounts) : purpose => var.academy_role_arn } : { for purpose, role in aws_iam_role.workload : purpose => role.arn }
    secret_arns      = merge(var.runtime_secret_arns[var.environment], { api = aws_secretsmanager_secret.api.arn })
  }
}
