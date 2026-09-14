mock_provider "aws" {
  mock_data "aws_eks_cluster" {
    defaults = {
      vpc_config = [{ vpc_id = "vpc-0123456789abcdef0" }]
      identity   = [{ oidc = [{ issuer = "https://oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE" }] }]
    }
  }
  mock_data "aws_iam_openid_connect_provider" {
    defaults = {
      url            = "oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE"
      client_id_list = ["sts.amazonaws.com"]
    }
  }
  mock_resource "aws_secretsmanager_secret" {
    defaults = { arn = "arn:aws:secretsmanager:us-east-1:123456789012:secret:oficina/staging/api/config-Ab1234" }
  }
}

variables {
  environment    = "staging"
  aws_account_id = "123456789012"
  platform = {
    contract_version  = 1
    aws_region        = "us-east-1"
    vpc_id            = "vpc-0123456789abcdef0"
    cluster_name      = "oficina-lab"
    oidc_provider_arn = "arn:aws:iam::123456789012:oidc-provider/oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE"
    oidc_issuer_url   = "https://oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE"
    environments = {
      staging  = { namespace = "oficina-staging", branch = "develop" }
      producao = { namespace = "oficina-producao", branch = "master" }
    }
  }
  database = {
    contract_version = 1
    aws_region       = "us-east-1"
    vpc_id           = "vpc-0123456789abcdef0"
    address          = "oficina-lab.example.us-east-1.rds.amazonaws.com"
    port             = 5432
    ssl_mode         = "VerifyFull"
    planned_environments = {
      staging = { database_name = "oficina_staging", app_role = "oficina_staging_app", migrations_role = "oficina_staging_migrations" }
    }
  }
  runtime_secret_arns = {
    staging = {
      app        = "arn:aws:secretsmanager:us-east-1:123456789012:secret:oficina/staging/database/app-Ab1234"
      migrations = "arn:aws:secretsmanager:us-east-1:123456789012:secret:oficina/staging/database/migrations-Ab1234"
    }
  }
}

run "separate_permissions_and_immutable_images" {
  command = apply
  assert {
    condition = (
      aws_ecr_repository.api.image_tag_mutability == "IMMUTABLE" && !aws_ecr_repository.api.force_delete &&
      aws_secretsmanager_secret.api.recovery_window_in_days == 30
    )
    error_message = "Imagens imutaveis e recuperacao de segredo devem permanecer habilitadas."
  }
  assert {
    condition = (
      jsondecode(aws_iam_role_policy.secrets["migrations"].policy).Statement[0].Resource == [var.runtime_secret_arns.staging.migrations] &&
      length(jsondecode(aws_iam_role_policy.secrets["app"].policy).Statement[0].Resource) == 2 &&
      contains(jsondecode(aws_iam_role_policy.secrets["app"].policy).Statement[0].Resource, var.runtime_secret_arns.staging.app) &&
      !contains(jsondecode(aws_iam_role_policy.secrets["app"].policy).Statement[0].Resource, var.runtime_secret_arns.staging.migrations)
    )
    error_message = "Roles devem ler apenas segredos de sua finalidade."
  }
  assert {
    condition = alltrue([for purpose, name in local.service_accounts :
      jsondecode(aws_iam_role.workload[purpose].assume_role_policy).Statement[0].Condition.StringEquals["${local.oidc_host}:sub"] == "system:serviceaccount:oficina-staging:${name}" &&
      jsondecode(aws_iam_role.workload[purpose].assume_role_policy).Statement[0].Condition.StringEquals["${local.oidc_host}:aud"] == "sts.amazonaws.com"
    ])
    error_message = "IRSA deve restringir namespace, service account e audience."
  }
}

run "reject_master_secret" {
  command = plan
  variables {
    runtime_secret_arns = { staging = {
      app        = "arn:aws:secretsmanager:us-east-1:123456789012:secret:rds!master-Ab1234"
      migrations = "arn:aws:secretsmanager:us-east-1:123456789012:secret:oficina/staging/database/migrations-Ab1234"
    } }
  }
  expect_failures = [var.runtime_secret_arns]
}

run "reject_wrong_vpc" {
  command = plan
  override_data {
    target = data.aws_eks_cluster.selected
    values = {
      vpc_config = [{ vpc_id = "vpc-99999999999999999" }]
      identity   = [{ oidc = [{ issuer = "https://oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE" }] }]
    }
  }
  expect_failures = [aws_ecr_repository.api]
}
