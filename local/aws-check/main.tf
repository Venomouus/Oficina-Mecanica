terraform {
  required_version = ">= 1.6"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  region  = "us-east-1"
  profile = "academy"
}

data "aws_caller_identity" "current" {}
data "aws_region" "current" {}
data "aws_iam_role" "lab" {
  name = "LabRole"
}

output "connected" {
  description = "Prova de que o Terraform autenticou; nao contem chaves."
  value = {
    account_id = data.aws_caller_identity.current.account_id
    arn        = data.aws_caller_identity.current.arn
    region     = data.aws_region.current.region
    lab_role   = data.aws_iam_role.lab.arn
    lab_role_trust = jsondecode(data.aws_iam_role.lab.assume_role_policy)
  }
}
