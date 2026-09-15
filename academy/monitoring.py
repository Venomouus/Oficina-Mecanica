"""Small demonstration stack: existing Grafana/Loki/Tempo plus Kubernetes metrics and health probes."""
import json
import secrets
import subprocess
from provision import ROOT
from kube import connect, apply

NAMESPACE = 'observability'
PROMETHEUS = '''global:
  scrape_interval: 15s
  scrape_native_histograms: true
otlp:
  keep_identifying_resource_attributes: true
  promote_resource_attributes: [service.name, service.instance.id, deployment.environment.name]
storage:
  tsdb:
    out_of_order_time_window: 10m
scrape_configs:
  - job_name: kubernetes-cadvisor
    scheme: https
    kubernetes_sd_configs:
      - role: node
    bearer_token_file: /var/run/secrets/kubernetes.io/serviceaccount/token
    tls_config:
      ca_file: /var/run/secrets/kubernetes.io/serviceaccount/ca.crt
    relabel_configs:
      - target_label: __address__
        replacement: kubernetes.default.svc:443
      - source_labels: [__meta_kubernetes_node_name]
        target_label: __metrics_path__
        replacement: /api/v1/nodes/$1/proxy/metrics/cadvisor
  - job_name: oficina-health
    metrics_path: /probe
    params:
      module: [http_2xx]
    static_configs:
      - targets:
          - http://oficina-api.oficina-staging.svc:8080/health
          - http://oficina-api.oficina-producao.svc:8080/health
    relabel_configs:
      - source_labels: [__address__]
        target_label: __param_target
      - source_labels: [__param_target]
        target_label: instance
      - target_label: __address__
        replacement: localhost:9115
'''


def main():
    connect()
    apply({'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': NAMESPACE}})
    apply({'apiVersion': 'v1', 'kind': 'ServiceAccount', 'metadata': {'name': 'oficina-monitoring', 'namespace': NAMESPACE}})
    apply({'apiVersion': 'rbac.authorization.k8s.io/v1', 'kind': 'ClusterRole', 'metadata': {'name': 'oficina-monitoring'},
        'rules': [{'apiGroups': [''], 'resources': ['nodes'], 'verbs': ['get', 'list', 'watch']},
                  {'apiGroups': [''], 'resources': ['nodes/proxy'], 'verbs': ['get']}]})
    apply({'apiVersion': 'rbac.authorization.k8s.io/v1', 'kind': 'ClusterRoleBinding', 'metadata': {'name': 'oficina-monitoring'},
        'roleRef': {'apiGroup': 'rbac.authorization.k8s.io', 'kind': 'ClusterRole', 'name': 'oficina-monitoring'},
        'subjects': [{'kind': 'ServiceAccount', 'name': 'oficina-monitoring', 'namespace': NAMESPACE}]})
    existing = subprocess.run(['kubectl', '-n', NAMESPACE, 'get', 'secret', 'grafana-admin', '-o', 'name'], capture_output=True)
    if existing.returncode:
        resource = {'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': 'grafana-admin', 'namespace': NAMESPACE},
                    'stringData': {'password': secrets.token_urlsafe(24)}}
        result = subprocess.run(['kubectl', 'apply', '--server-side', '-f', '-'], input=json.dumps(resource), text=True, capture_output=True)
        if result.returncode:
            raise RuntimeError('Falha ao configurar senha Grafana; valor omitido.')
    dashboard = json.loads((ROOT / 'local/grafana/dashboards/oficina.json').read_text(encoding='utf-8'))
    dashboard['title'] = 'Oficina - AWS Academy'
    y = max(p['gridPos']['y'] + p['gridPos']['h'] for p in dashboard['panels'])
    for index, (title, expr, unit) in enumerate([
        ('CPU Kubernetes por pod', 'sum by (namespace,pod) (rate(container_cpu_usage_seconds_total{namespace=~"oficina-.*",container!="",container!="POD"}[2m]))', 'cores'),
        ('Memoria Kubernetes por pod', 'sum by (namespace,pod) (container_memory_working_set_bytes{namespace=~"oficina-.*",container!="",container!="POD"})', 'bytes'),
        ('Healthcheck / uptime', 'probe_success{job="oficina-health"}', 'short')]):
        dashboard['panels'].append({'id': 100 + index, 'title': title, 'type': 'timeseries',
            'datasource': {'type': 'prometheus', 'uid': 'oficina-prom'}, 'gridPos': {'x': index * 8, 'y': y, 'w': 8, 'h': 8},
            'targets': [{'refId': 'A', 'expr': expr}], 'fieldConfig': {'defaults': {'unit': unit}, 'overrides': []}})
    data = {'prometheus.yaml': PROMETHEUS, 'oficina.json': json.dumps(dashboard),
            'blackbox.yaml': 'modules:\n  http_2xx:\n    prober: http\n    timeout: 4s\n    http:\n      preferred_ip_protocol: ip4\n'}
    mounts = []
    for folder in ('datasources', 'dashboards', 'alerting'):
        name = folder + '.yaml'
        data[name] = (ROOT / 'local/grafana/provisioning' / folder / 'oficina.yaml').read_text(encoding='utf-8')
        mounts.append({'name': 'config', 'mountPath': '/etc/grafana/provisioning/' + folder + '/oficina.yaml', 'subPath': name, 'readOnly': True})
    apply({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'oficina-monitoring', 'namespace': NAMESPACE}, 'data': data})
    mounts += [{'name': 'config', 'mountPath': path, 'subPath': name, 'readOnly': True} for name, path in
        [('prometheus.yaml', '/otel-lgtm/prometheus.yaml'), ('oficina.json', '/demo-dashboards/oficina.json')]]
    apply({'apiVersion': 'apps/v1', 'kind': 'Deployment', 'metadata': {'name': 'lgtm', 'namespace': NAMESPACE},
        'spec': {'replicas': 1, 'strategy': {'type': 'Recreate'}, 'selector': {'matchLabels': {'app': 'lgtm'}},
            'template': {'metadata': {'labels': {'app': 'lgtm'}}, 'spec': {'serviceAccountName': 'oficina-monitoring',
                'containers': [{'name': 'lgtm', 'image': 'grafana/otel-lgtm:0.33.0',
                    'env': [{'name': 'GF_PATHS_PROVISIONING', 'value': '/etc/grafana/provisioning'},
                            {'name': 'GF_AUTH_ANONYMOUS_ENABLED', 'value': 'false'},
                            {'name': 'GF_SECURITY_ADMIN_PASSWORD', 'valueFrom': {'secretKeyRef': {'name': 'grafana-admin', 'key': 'password'}}}],
                    'resources': {'requests': {'cpu': '250m', 'memory': '1Gi'}, 'limits': {'cpu': '1500m', 'memory': '2Gi'}},
                    'readinessProbe': {'httpGet': {'path': '/api/health', 'port': 3000}, 'initialDelaySeconds': 30, 'periodSeconds': 10},
                    'volumeMounts': mounts + [{'name': 'data', 'mountPath': '/data'}]},
                    {'name': 'health-probe', 'image': 'prom/blackbox-exporter:v0.27.0', 'args': ['--config.file=/config/blackbox.yaml'],
                     'volumeMounts': [{'name': 'config', 'mountPath': '/config', 'readOnly': True}],
                     'resources': {'requests': {'cpu': '25m', 'memory': '32Mi'}, 'limits': {'cpu': '100m', 'memory': '64Mi'}}}],
                'volumes': [{'name': 'config', 'configMap': {'name': 'oficina-monitoring'}}, {'name': 'data', 'emptyDir': {'sizeLimit': '5Gi'}}]}}}})
    apply({'apiVersion': 'v1', 'kind': 'Service', 'metadata': {'name': 'lgtm', 'namespace': NAMESPACE},
        'spec': {'selector': {'app': 'lgtm'}, 'ports': [{'name': name, 'port': port, 'targetPort': port} for name, port in
            [('grafana', 3000), ('otlp', 4317), ('prometheus', 9090), ('loki', 3100), ('tempo', 3200)]]}})
    subprocess.run(['kubectl', '-n', NAMESPACE, 'rollout', 'status', 'deployment/lgtm', '--timeout=600s'], check=True)
    print('Monitoramento pronto. Acesso Grafana por port-forward; dados temporarios para demonstracao.')


if __name__ == '__main__':
    main()
