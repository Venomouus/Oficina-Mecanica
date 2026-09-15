"""Publish the API and run the existing database bootstrap in the private cluster."""
import argparse
import base64
import datetime
import json
import secrets
import subprocess
import sys
from provision import ROOT, WORK, DATABASE, SERVERLESS, SESSION, outputs, command
from kube import connect, apply


def secret(arn, factory):
    client = SESSION.client('secretsmanager')
    try:
        return json.loads(client.get_secret_value(SecretId=arn)['SecretString'])
    except client.exceptions.ResourceNotFoundException:
        value = factory()
        client.put_secret_value(SecretId=arn, SecretString=json.dumps(value))
        return value


def key(env):
    path = WORK / ('jwt-' + env + '.local.pem')
    if not path.exists():
        generator = SERVERLESS / 'src/Oficina.Autenticacao.Local/bin/Debug/net8.0/Oficina.Autenticacao.Local.dll'
        command(['dotnet', str(generator), '--generate-dev-key', str(path)], 'generate-key-' + env)
    return {'privateKeyPem': path.read_text(), 'keyId': 'academy-' + env + '-1'}


def initialize_secrets(env):
    database = outputs('database')
    api = outputs('api-' + env)['api']
    client = SESSION.client('secretsmanager')
    auth = {'secret_arns': {purpose: client.describe_secret(
        SecretId='/oficina/' + env + '/autenticacao/' + purpose)['ARN'] for purpose in ('database', 'jwt')}}
    roles = database['database']['planned_environments'][env]
    credentials = {}
    for purpose in ('app', 'migrations', 'auth'):
        arn = auth['secret_arns']['database'] if purpose == 'auth' else api['secret_arns'][purpose]
        credentials[purpose] = secret(arn, lambda: {
            'username': roles[purpose + '_role'], 'password': secrets.token_urlsafe(32)})
    secret(auth['secret_arns']['jwt'], lambda: key(env))
    secret(api['secret_arns']['api'], lambda: {'jwtKey': secrets.token_urlsafe(48),
        'adminUser': 'admin', 'adminPassword': secrets.token_urlsafe(24), 'budgetToken': secrets.token_urlsafe(32)})
    print('Segredos preparados; valores omitidos.')
    return credentials


def bootstrap(env, phase, credentials):
    database = outputs('database')
    namespace = 'oficina-' + env
    master = json.loads(SESSION.client('secretsmanager').get_secret_value(
        SecretId=database['bootstrap_secret_arn'])['SecretString'])
    values = {'BOOTSTRAP_ADMIN_USERNAME': master['username'], 'BOOTSTRAP_ADMIN_PASSWORD': master['password']}
    values.update({'BOOTSTRAP_' + p.upper() + '_PASSWORD': c['password'] for p, c in credentials.items()})
    temporary = {'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': 'database-bootstrap', 'namespace': namespace},
                 'type': 'Opaque', 'stringData': values}
    result = subprocess.run(['kubectl', 'apply', '--server-side', '-f', '-'], input=json.dumps(temporary),
                            text=True, capture_output=True)
    if result.returncode:
        raise RuntimeError('Nao foi possivel criar o segredo temporario do bootstrap.')
    apply({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'database-bootstrap', 'namespace': namespace},
        'data': {'bootstrap.py': (DATABASE / 'bootstrap/bootstrap.py').read_text(encoding='utf-8'),
                 'database.json': json.dumps(database['database']),
                 'ca.pem': (ROOT / 'Oficina.API/certificates/rds-global-bundle.pem').read_text()}})
    name = 'database-' + phase + '-' + datetime.datetime.now(datetime.timezone.utc).strftime('%H%M%S')
    apply({'apiVersion': 'batch/v1', 'kind': 'Job', 'metadata': {'name': name, 'namespace': namespace},
        'spec': {'backoffLimit': 0, 'activeDeadlineSeconds': 600, 'ttlSecondsAfterFinished': 3600,
            'template': {'spec': {'restartPolicy': 'Never', 'automountServiceAccountToken': False,
                'containers': [{'name': 'bootstrap', 'image': 'postgres:16-alpine',
                    'command': ['sh', '-ec', 'apk add --no-cache python3 >/dev/null && exec python3 /config/bootstrap.py '
                        '--contract /config/database.json --environment ' + env + ' --phase ' + phase + ' --ssl-root-cert /config/ca.pem'],
                    'envFrom': [{'secretRef': {'name': 'database-bootstrap'}}],
                    'resources': {'requests': {'cpu': '100m', 'memory': '128Mi'}, 'limits': {'cpu': '500m', 'memory': '256Mi'}},
                    'volumeMounts': [{'name': 'config', 'mountPath': '/config', 'readOnly': True}]}],
                'volumes': [{'name': 'config', 'configMap': {'name': 'database-bootstrap'}}]}}}})
    result = subprocess.run(['kubectl', '-n', namespace, 'wait', '--for=condition=complete', 'job/' + name, '--timeout=600s'])
    subprocess.run(['kubectl', '-n', namespace, 'logs', 'job/' + name], check=False)
    subprocess.run(['kubectl', '-n', namespace, 'delete', 'secret', 'database-bootstrap'], check=True)
    if result.returncode:
        raise RuntimeError('Bootstrap falhou; verificar o log acima.')


def publish(env):
    api = outputs('api-' + env)['api']
    ecr = SESSION.client('ecr')
    auth = ecr.get_authorization_token()['authorizationData'][0]
    user, password = base64.b64decode(auth['authorizationToken']).decode().split(':', 1)
    result = subprocess.run(['docker', 'login', '--username', user, '--password-stdin', auth['proxyEndpoint']],
                            input=password, text=True, capture_output=True)
    if result.returncode:
        raise RuntimeError('Login ECR falhou (credencial omitida).')
    release = datetime.datetime.now(datetime.timezone.utc).strftime('r%Y%m%d%H%M%S')
    tag = api['image_repository'] + ':' + release
    command(['docker', 'tag', 'oficina-academy-api:entrega', tag], 'tag-' + env)
    command(['docker', 'push', tag], 'push-' + env)
    repository = api['image_repository'].split('/', 1)[1]
    digest = ecr.describe_images(repositoryName=repository, imageIds=[{'imageTag': release}])['imageDetails'][0]['imageDigest']
    platform = outputs('platform')['platform']
    ec2 = SESSION.client('ec2')
    cidrs = lambda ids: [s['CidrBlock'] for s in ec2.describe_subnets(SubnetIds=ids)['Subnets']]
    config = {'api': api, 'gateway': outputs('gateway-' + env)['gateway'],
              'backend': outputs('backend-' + env)['backend'], 'image_digest': digest, 'release': release,
              'alb_subnet_cidrs': cidrs(platform['private_subnet_ids']),
              'database_subnet_cidrs': cidrs(platform['database_subnet_ids']),
              'otlp_endpoint': 'http://lgtm.observability.svc.cluster.local:4317'}
    path = WORK / ('deployment-' + env + '.local.json')
    path.write_text(json.dumps(config, indent=2))
    print('Imagem publicada por digest; contrato salvo em ' + str(path))
    return config


def deploy(env):
    connect()
    namespace = 'oficina-' + env
    apply({'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': namespace}})
    credentials = initialize_secrets(env)
    config = json.loads((WORK / ('deployment-' + env + '.local.json')).read_text())
    command([sys.executable, str(ROOT / 'academy/sync-secrets.py'), '--config',
             str(WORK / ('deployment-' + env + '.local.json'))], 'sync-' + env)
    sys.path.insert(0, str(ROOT / 'deploy'))
    from render import render
    manifests = render(config)
    bootstrap(env, 'prepare', credentials)
    apply(manifests['setup.json'])
    apply(manifests['migration.json'])
    subprocess.run(['kubectl', '-n', namespace, 'wait', '--for=condition=complete',
        'job/' + manifests['migration.json']['metadata']['name'], '--timeout=600s'], check=True)
    bootstrap(env, 'grants', credentials)
    apply(manifests['api.json'])
    subprocess.run(['kubectl', 'apply', '-f', '-'], input=outputs('backend-' + env)['kubernetes_manifest'], text=True, check=True)
    subprocess.run(['kubectl', '-n', namespace, 'rollout', 'status', 'deployment/oficina-api', '--timeout=300s'], check=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('step', choices=['secrets', 'publish', 'deploy'])
    parser.add_argument('--environment', choices=['staging', 'producao'], default='staging')
    args = parser.parse_args()
    {'secrets': initialize_secrets, 'publish': publish, 'deploy': deploy}[args.step](args.environment)
