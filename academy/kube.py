"""Local EKS access and the existing Load Balancer Controller configuration."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import urllib.request
import zipfile
from provision import ROOT, WORK, REGION, SESSION, outputs


def download(url, destination):
    if not destination.exists():
        data = urllib.request.urlopen(url, timeout=120).read()
        digest = urllib.request.urlopen(url + '.sha256', timeout=30).read().decode().split()[0]
        if hashlib.sha256(data).hexdigest() != digest:
            raise RuntimeError('Checksum incorreto: ' + destination.name)
        destination.write_bytes(data)


def connect():
    platform = outputs('platform')['platform']
    cluster = SESSION.client('eks').describe_cluster(name=platform['cluster_name'])['cluster']
    if cluster['status'] != 'ACTIVE':
        raise RuntimeError('Aguardar cluster ACTIVE.')
    arn = cluster['arn']
    config = {'apiVersion': 'v1', 'kind': 'Config', 'current-context': arn,
        'clusters': [{'name': arn, 'cluster': {'server': cluster['endpoint'],
                     'certificate-authority-data': cluster['certificateAuthority']['data']}}],
        'contexts': [{'name': arn, 'context': {'cluster': arn, 'user': arn}}],
        'users': [{'name': arn, 'user': {'exec': {'apiVersion': 'client.authentication.k8s.io/v1beta1',
            'command': sys.executable, 'args': [str(ROOT / 'academy/eks-token.py'), cluster['name']],
            'interactiveMode': 'Never', 'env': [{'name': 'AWS_PROFILE', 'value': os.environ.get('AWS_PROFILE', 'academy')}]}}}]}
    path = WORK / 'kubeconfig.local.json'
    path.write_text(json.dumps(config), encoding='utf-8')
    os.environ['KUBECONFIG'] = str(path)
    if os.name == 'nt':
        version = urllib.request.urlopen('https://dl.k8s.io/release/stable-' + cluster['version'] + '.txt', timeout=30).read().decode().strip()
        kubectl = WORK / 'kubectl.exe'
        download('https://dl.k8s.io/release/' + version + '/bin/windows/amd64/kubectl.exe', kubectl)
        os.environ['PATH'] = str(WORK) + os.pathsep + os.environ['PATH']
    return platform


def apply(resource):
    subprocess.run(['kubectl', 'apply', '-f', '-'], input=json.dumps(resource), text=True, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('step', choices=['connect', 'controller'])
    args = parser.parse_args()
    platform = connect()
    subprocess.run(['kubectl', 'get', 'nodes'], check=True)
    if args.step == 'controller':
        helm = 'helm'
        if os.name == 'nt':
            archive = WORK / 'helm.zip'
            download('https://get.helm.sh/helm-v3.19.0-windows-amd64.zip', archive)
            with zipfile.ZipFile(archive) as bundle:
                (WORK / 'helm.exe').write_bytes(bundle.read('windows-amd64/helm.exe'))
            helm = str(WORK / 'helm.exe')
        for item in platform['environments'].values():
            apply({'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': item['namespace']}})
        values = WORK / 'controller-values.local.yaml'
        values.write_text(json.dumps({'clusterName': platform['cluster_name'], 'region': REGION,
            'vpcId': platform['vpc_id'], 'replicaCount': 2, 'hostNetwork': True,
            'dnsPolicy': 'ClusterFirstWithHostNet', 'enableShield': False, 'enableWaf': False,
            'enableWafv2': False, 'enableServiceMutatorWebhook': False,
            'enableBackendSecurityGroup': False, 'serviceAccount': {'create': True,
            'name': 'aws-load-balancer-controller', 'annotations': {}}}), encoding='utf-8')
        subprocess.run([helm, 'repo', 'add', 'eks', 'https://aws.github.io/eks-charts'], check=True)
        subprocess.run([helm, 'repo', 'update', 'eks'], check=True)
        subprocess.run([helm, 'upgrade', '--install', 'aws-load-balancer-controller', 'eks/aws-load-balancer-controller',
                        '--namespace', 'kube-system', '--version', '1.14.0', '-f', str(values), '--wait', '--timeout', '5m'], check=True)


if __name__ == '__main__':
    main()
