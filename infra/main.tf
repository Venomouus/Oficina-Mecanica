terraform {
  required_version = ">= 1.6.0"

  required_providers {
    null = {
      source  = "hashicorp/null"
      version = "~> 3.2"
    }
  }
}

locals {
  project_root = abspath("${path.module}/..")

  manifest_files = [
    "k8s/namespace.yaml",
    "k8s/configmap.yaml",
    "k8s/secret.yaml",
    "k8s/postgres-deployment.yaml",
    "k8s/postgres-service.yaml",
    "k8s/api-deployment.yaml",
    "k8s/api-service.yaml",
    "k8s/hpa.yaml"
  ]

  manifest_paths   = [for file in local.manifest_files : abspath("${local.project_root}/${file}")]
  manifests_hash   = sha256(join("", [for file in local.manifest_files : filesha256("${local.project_root}/${file}")]))
  kind_config_path = abspath("${path.module}/kind-config.yaml")
  kind_context     = "kind-${var.cluster_name}"
}

resource "null_resource" "kind_cluster" {
  triggers = {
    cluster_name = var.cluster_name
    config_hash  = filesha256(local.kind_config_path)
  }

  provisioner "local-exec" {
    interpreter = var.command_interpreter
    command     = <<-EOT
      $ErrorActionPreference = "Stop"
      $clusterName = "${var.cluster_name}"
      $clusterExists = kind get clusters | Where-Object { $_ -eq $clusterName }

      if (-not $clusterExists) {
        kind create cluster --name $clusterName --config "${local.kind_config_path}"
      }
      else {
        Write-Host "Cluster $clusterName already exists."
      }

      kubectl config use-context "${local.kind_context}"
    EOT
  }

  provisioner "local-exec" {
    when        = destroy
    interpreter = ["PowerShell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]
    command     = "kind delete cluster --name ${self.triggers.cluster_name}"
  }
}

resource "null_resource" "kubernetes_stack" {
  count      = var.apply_manifests ? 1 : 0
  depends_on = [null_resource.kind_cluster]

  triggers = {
    app_image      = var.app_image
    manifests_hash = local.manifests_hash
  }

  provisioner "local-exec" {
    interpreter = var.command_interpreter
    command     = <<-EOT
      $ErrorActionPreference = "Stop"

      kubectl config use-context "${local.kind_context}"

      docker build --progress=plain -t "${var.app_image}" "${local.project_root}"
      kind load docker-image "${var.app_image}" --name "${var.cluster_name}"

      $manifests = @(
        ${join(",\n        ", [for path in local.manifest_paths : "\"${path}\""])}
      )

      foreach ($manifest in $manifests) {
        kubectl apply -f $manifest
      }

      kubectl rollout status deployment/oficina-postgres -n oficina --timeout=120s
      kubectl rollout status deployment/oficina-api -n oficina --timeout=120s
    EOT
  }
}