terraform {
  required_version = ">= 1.10, < 2.0"
  required_providers {
    aws = { source = "hashicorp/aws", version = "= 6.64.0" }
  }
}

provider "aws" {
  region              = "us-east-1"
  allowed_account_ids = [var.aws_account_id]
  default_tags {
    tags = { Project = "oficina", ManagedBy = "Terraform", Scope = "academy" }
  }
}

variable "aws_account_id" {
  type = string
  validation {
    condition     = can(regex("^[0-9]{12}$", var.aws_account_id))
    error_message = "Informe a conta do laboratorio."
  }
}

locals {
  state_bucket = "oficina-tfstate-${var.aws_account_id}-us-east-1"
}

# Academy denies GetBucketObjectLockConfiguration, which aws_s3_bucket reads.
# The existing bootstrap bucket is retained; only supported settings are managed.
removed {
  from = aws_s3_bucket.state
  lifecycle { destroy = false }
}
resource "aws_s3_bucket_public_access_block" "state" {
  bucket                  = local.state_bucket
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}
resource "aws_s3_bucket_versioning" "state" {
  bucket = local.state_bucket
  versioning_configuration { status = "Enabled" }
}
resource "aws_s3_bucket_server_side_encryption_configuration" "state" {
  bucket = local.state_bucket
  rule {
    apply_server_side_encryption_by_default { sse_algorithm = "AES256" }
  }
}
output "state_bucket" { value = local.state_bucket }
