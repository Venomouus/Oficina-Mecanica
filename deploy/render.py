"""Render public Kubernetes JSON manifests only. No AWS/kubectl calls, credentials or deploy."""
import argparse
import ipaddress
import json
from pathlib import Path
import re


def require(condition, message):
    if not condition:
        raise ValueError(message)


def render(config):
    api, gateway, backend = (config[key] for key in ("api", "gateway", "backend"))
    env = api["environment"]
    require(env in ("staging", "producao"), "Ambiente invalido")
    namespace = f"oficina-{env}"
    require(all(c["contract_version"] == 1 and c["environment"] == env
                and c["aws_region"] == api["aws_region"] for c in (api, gateway, backend)), "Contratos incompatíveis")
    require(api["namespace"] == backend["namespace"] == namespace
            and api["cluster_name"] == backend["cluster_name"], "Cluster/namespace incompatível")
    account, region = api["aws_account_id"], api["aws_region"]
    require(re.fullmatch(r"\d{12}", account), "Conta invalida")
    require(re.fullmatch(r"[a-z]{2}(?:-[a-z]+)+-\d", region), "Regiao invalida")
    require(re.fullmatch(rf"{account}\.dkr\.ecr\.{region}\.amazonaws\.com/[a-z0-9/-]+", api["image_repository"]), "ECR invalido")
    require(re.fullmatch(r"sha256:[0-9a-f]{64}", config["image_digest"]), "Use digest sha256 imutavel")
    require(re.fullmatch(r"[a-z0-9][a-z0-9-]{0,29}", config["release"]), "Release invalida")
    require(re.fullmatch(r"[A-Za-z0-9.-]+\.rds\.amazonaws\.com", api["database_host"]), "Host RDS invalido")
    require(api["database_name"] == f"oficina_{env}", "Banco de outro ambiente")
    issuer = gateway["issuer"]
    require(re.fullmatch(rf"https://[a-z0-9]+\.execute-api\.{region}\.amazonaws\.com", issuer)
            and gateway["audience"] == "oficina-api", "Emissor Gateway invalido")
    require(api["role_arns"]["app"] != api["role_arns"]["migrations"], "Roles devem ser separados")
    for role in api["role_arns"].values():
        require(re.fullmatch(rf"arn:aws:iam::{account}:role/[A-Za-z0-9+=,.@_/-]+", role), "Role de outra conta")
    for purpose in ("app", "migrations", "api"):
        suffix = f"database/{purpose}" if purpose != "api" else "api/config"
        require(re.fullmatch(rf"arn:aws:secretsmanager:{region}:{account}:secret:[a-z0-9-]+/{env}/{suffix}-[A-Za-z0-9]{{6}}",
                             api["secret_arns"][purpose]), "Segredo de outra finalidade/ambiente")
    private_ranges = [ipaddress.ip_network(cidr) for cidr in ("10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16")]
    for field in ("alb_subnet_cidrs", "database_subnet_cidrs"):
        require(len(config[field]) >= 2, "Informe as subnets privadas de pelo menos duas AZs")
        for cidr in config[field]:
            network = ipaddress.ip_network(cidr)
            require(network.version == 4 and any(network.subnet_of(r) for r in private_ranges), "CIDR deve ser privado RFC1918")

    def resource(kind, name, spec=None, version="v1", **extra):
        result = {"apiVersion": version, "kind": kind, "metadata": {"name": name, "namespace": namespace}, **extra}
        if spec is not None:
            result["spec"] = spec
        return result

    common = {
        "ASPNETCORE_ENVIRONMENT": "Production", "ASPNETCORE_URLS": "http://+:8080",
        "AwsRuntime__Enabled": "true", "AwsRuntime__DatabaseHost": api["database_host"],
        "AwsRuntime__Database": api["database_name"], "Database__MigrateOnStartup": "false",
        "AWS_REGION": region, "AWS_DEFAULT_REGION": region, "AWS_STS_REGIONAL_ENDPOINTS": "regional",
        "AWS_EC2_METADATA_DISABLED": "true", "DOTNET_EnableDiagnostics": "0",
    }
    runtime = {**common, "AwsRuntime__DatabaseSecretArn": api["secret_arns"]["app"],
               "AwsRuntime__ApiSecretArn": api["secret_arns"]["api"],
               "ClienteJwt__Enabled": "true", "ClienteJwt__Issuer": issuer,
               "ClienteJwt__Audience": gateway["audience"], "Jwt__Issuer": f"oficina-{env}",
               "Jwt__Audience": f"oficina-{env}-admin"}
    migration = {**common, "AwsRuntime__DatabaseSecretArn": api["secret_arns"]["migrations"]}
    image = f"{api['image_repository']}@{config['image_digest']}"

    def pod(purpose, values):
        service_account = "oficina-api" if purpose == "app" else "oficina-migrations"
        container = {
            "name": service_account, "image": image, "imagePullPolicy": "IfNotPresent",
            "env": [{"name": name, "value": value} for name, value in sorted(values.items())],
            "securityContext": {"allowPrivilegeEscalation": False, "readOnlyRootFilesystem": True,
                                "capabilities": {"drop": ["ALL"]}},
            "resources": {"requests": {"cpu": "100m", "memory": "256Mi"},
                          "limits": {"cpu": "500m", "memory": "512Mi"}},
            "volumeMounts": [{"name": "tmp", "mountPath": "/tmp"}],
        }
        return {"metadata": {"labels": {"app": service_account}, "annotations": {"oficina/release": config["release"]}},
                "spec": {"serviceAccountName": service_account, "automountServiceAccountToken": False,
                         "securityContext": {"runAsNonRoot": True, "runAsUser": 1654, "runAsGroup": 1654,
                                             "fsGroup": 1654, "seccompProfile": {"type": "RuntimeDefault"}},
                         "terminationGracePeriodSeconds": 30,
                         "containers": [container], "volumes": [{"name": "tmp", "emptyDir": {"sizeLimit": "64Mi"}}]}}

    setup = []
    for purpose, name in (("app", "oficina-api"), ("migrations", "oficina-migrations")):
        sa = resource("ServiceAccount", name, automountServiceAccountToken=False)
        sa["metadata"]["annotations"] = {"eks.amazonaws.com/role-arn": api["role_arns"][purpose],
                                          "eks.amazonaws.com/sts-regional-endpoints": "true"}
        setup.append(sa)
        ingress = [] if purpose == "migrations" else [{"from": [{"ipBlock": {"cidr": cidr}} for cidr in config["alb_subnet_cidrs"]],
                                                       "ports": [{"protocol": "TCP", "port": 8080}]}]
        setup.append(resource("NetworkPolicy", name, {
            "podSelector": {"matchLabels": {"app": name}}, "policyTypes": ["Ingress", "Egress"], "ingress": ingress,
            "egress": [
                {"to": [{"namespaceSelector": {"matchLabels": {"kubernetes.io/metadata.name": "kube-system"}},
                         "podSelector": {"matchLabels": {"k8s-app": "kube-dns"}}}],
                 "ports": [{"protocol": protocol, "port": 53} for protocol in ("UDP", "TCP")]},
                {"to": [{"ipBlock": {"cidr": cidr}} for cidr in config["database_subnet_cidrs"]],
                 "ports": [{"protocol": "TCP", "port": 5432}]},
                {"to": [{"ipBlock": {"cidr": "0.0.0.0/0", "except": ["169.254.0.0/16", "127.0.0.0/8"]}}],
                 "ports": [{"protocol": "TCP", "port": 443}]},
            ]}, "networking.k8s.io/v1"))

    template = pod("migrations", migration)
    template["spec"]["restartPolicy"] = "Never"
    template["spec"]["containers"][0]["args"] = ["--migrate"]
    job = resource("Job", f"oficina-migrate-{config['release']}", {
        "backoffLimit": 0, "activeDeadlineSeconds": 600, "ttlSecondsAfterFinished": 86400, "template": template}, "batch/v1")

    template = pod("app", runtime)
    template["spec"]["topologySpreadConstraints"] = [{
        "maxSkew": 1, "topologyKey": "kubernetes.io/hostname", "whenUnsatisfiable": "ScheduleAnyway",
        "labelSelector": {"matchLabels": {"app": "oficina-api"}}}]
    container = template["spec"]["containers"][0]
    container["ports"] = [{"name": "http", "containerPort": 8080}]
    for name, path, failures in (("startupProbe", "/health/live", 30), ("livenessProbe", "/health/live", 3),
                                 ("readinessProbe", "/health", 3)):
        container[name] = {"httpGet": {"path": path, "port": "http"}, "periodSeconds": 5,
                           "timeoutSeconds": 4, "failureThreshold": failures}
    # replicas deliberately omitted: HPA owns scale after initial apply.
    deployment = resource("Deployment", "oficina-api", {
        "selector": {"matchLabels": {"app": "oficina-api"}}, "template": template,
        "progressDeadlineSeconds": 300, "revisionHistoryLimit": 3,
        "strategy": {"type": "RollingUpdate", "rollingUpdate": {"maxSurge": 1, "maxUnavailable": 0}}}, "apps/v1")
    hpa = resource("HorizontalPodAutoscaler", "oficina-api", {
        "scaleTargetRef": {"apiVersion": "apps/v1", "kind": "Deployment", "name": "oficina-api"},
        "minReplicas": 2, "maxReplicas": 4,
        "metrics": [{"type": "Resource", "resource": {"name": "cpu", "target": {"type": "Utilization", "averageUtilization": 70}}}],
        "behavior": {"scaleDown": {"stabilizationWindowSeconds": 300}}}, "autoscaling/v2")
    pdb = resource("PodDisruptionBudget", "oficina-api", {
        "minAvailable": 1, "selector": {"matchLabels": {"app": "oficina-api"}}}, "policy/v1")
    as_list = lambda items: {"apiVersion": "v1", "kind": "List", "items": items}
    return {"setup.json": as_list(setup), "migration.json": job, "api.json": as_list([deployment, hpa, pdb])}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=Path("deploy/generated"))
    arguments = parser.parse_args()
    try:
        manifests = render(json.loads(arguments.config.read_text(encoding="utf-8-sig")))
        arguments.output.mkdir(parents=True, exist_ok=True)
        for filename, manifest in manifests.items():
            (arguments.output / filename).write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        print("Manifests gerados. Nenhum recurso aplicado.")
    except (KeyError, ValueError, TypeError):
        parser.exit(1, "Configuracao publica invalida; confira os contratos v1 e o exemplo.\n")
