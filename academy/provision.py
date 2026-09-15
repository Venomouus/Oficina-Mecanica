"""Provision existing Terraform roots in dependency order, with reviewed saved plans.
Defaults to plan. --apply creates chargeable AWS resources in the Academy account.
"""
import argparse
import collections
import json
import os
from pathlib import Path
import subprocess
import sys
import urllib.request

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'local/.aws-sdk.local'))
import boto3

ACCOUNT = '775213651200'
REGION = 'us-east-1'
BUCKET = f'oficina-tfstate-{ACCOUNT}-{REGION}'
WORK = ROOT / 'academy/.work.local'
PLATFORM = ROOT.parent / 'Oficina-Kubernetes/Oficina-infra-kubernetes'
DATABASE = ROOT.parent / 'Oficina-Database/Oficina-infra-database'
SERVERLESS = ROOT.parent / 'Oficina-Serverless/Oficina-serverless'
os.environ.setdefault('AWS_PROFILE', 'academy')
os.environ.setdefault('AWS_REGION', REGION)
SESSION = boto3.Session(profile_name=os.environ['AWS_PROFILE'], region_name=REGION)


def command(args, name):
    result = subprocess.run(args, capture_output=True, text=True, encoding='utf-8', errors='replace')
    log = WORK / (name + '.local.log')
    log.write_text(result.stdout + result.stderr, encoding='utf-8')
    if result.returncode:
        print((result.stderr or result.stdout)[-2500:], flush=True)
        raise RuntimeError(f'{name} falhou. Log: {log}')
    return result.stdout


def terraform(root, key, variables, apply=False, bootstrap=False):
    WORK.mkdir(parents=True, exist_ok=True)
    prefix = ['terraform', '-chdir=' + str(root)]
    variable_file = WORK / (key + '.tfvars.json')
    variable_file.write_text(json.dumps(variables, indent=2), encoding='utf-8')
    init = prefix + ['init', '-input=false', '-no-color']
    cache = ROOT / 'local/aws-check/.terraform/providers'
    if os.name == 'nt' and cache.exists():
        init.append('-plugin-dir=' + str(cache))
    if not bootstrap:
        init += ['-reconfigure', '-backend-config=bucket=' + BUCKET, '-backend-config=key=academy/' + key + '.tfstate',
                 '-backend-config=region=' + REGION, '-backend-config=use_lockfile=true', '-backend-config=encrypt=true']
    command(init, key + '-init')
    plan = WORK / (key + '.tfplan')
    plan_args = prefix + ['plan', '-input=false', '-no-color', '-var-file=' + str(variable_file), '-out=' + str(plan)]
    if bootstrap:
        plan_args.append('-refresh=false')  # forget the bucket without the SCP-denied ObjectLock read
    command(plan_args, key + '-plan')
    details = json.loads(command(prefix + ['show', '-json', str(plan)], key + '-review'))
    actions = collections.Counter()
    changes = []
    for resource in details.get('resource_changes', []):
        action = resource['change']['actions']
        if 'delete' in action:
            raise RuntimeError('Plano inclui exclusao/substituicao; revisao manual necessaria: ' + resource['address'])
        actions.update(action)
        if action != ['no-op'] and resource.get('mode') == 'managed':
            changes.append(resource['address'])
    print(f'{key}: {dict(actions)}', flush=True)
    print('Recursos: ' + ', '.join(changes), flush=True)
    if not apply:
        return None
    print('Aplicando ' + key + '...', flush=True)
    command(prefix + ['apply', '-input=false', '-no-color', str(plan)], key + '-apply')
    output = json.loads(command(prefix + ['output', '-json'], key + '-outputs'))
    values = {k: v['value'] for k, v in output.items()}
    (WORK / (key + '.outputs.local.json')).write_text(json.dumps(values, indent=2), encoding='utf-8')
    print(key + ' aplicado.', flush=True)
    return values


def outputs(key):
    path = WORK / (key + '.outputs.local.json')
    if path.exists():
        return json.loads(path.read_text(encoding='utf-8'))
    state = json.loads(SESSION.client('s3').get_object(Bucket=BUCKET, Key='academy/' + key + '.tfstate')['Body'].read())
    values = {k: v['value'] for k, v in state['outputs'].items()}
    WORK.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(values, indent=2), encoding='utf-8')
    return values


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('step', choices=['bootstrap', 'platform', 'database', 'gateway', 'api', 'authentication', 'backend'])
    parser.add_argument('--environment', choices=['staging', 'producao'], default='staging')
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--jwt-ready', action='store_true')
    args = parser.parse_args()
    if SESSION.client('sts').get_caller_identity()['Account'] != ACCOUNT:
        raise RuntimeError('Conta diferente do laboratorio autorizado.')
    shared = {'aws_account_id': ACCOUNT, 'aws_region': REGION, 'project_name': 'oficina'}
    lab_role = f'arn:aws:iam::{ACCOUNT}:role/LabRole'
    if args.step == 'bootstrap':
        s3 = SESSION.client('s3')
        try:
            s3.head_bucket(Bucket=BUCKET)
        except s3.exceptions.ClientError as error:
            if error.response['ResponseMetadata']['HTTPStatusCode'] != 404:
                raise
            if not args.apply:
                raise RuntimeError('Bucket de bootstrap ainda inexistente. Usar --apply para cria-lo.')
            s3.create_bucket(Bucket=BUCKET)
        terraform(ROOT / 'academy/bootstrap', 'bootstrap', {'aws_account_id': ACCOUNT}, args.apply, bootstrap=True)
    elif args.step == 'platform':
        ip = urllib.request.urlopen('https://checkip.amazonaws.com', timeout=15).read().decode().strip()
        import ipaddress
        ipaddress.IPv4Address(ip)
        terraform(PLATFORM / 'infra', 'platform', dict(shared, academy_role_arn=lab_role,
            cluster_admin_principal_arns=[f'arn:aws:iam::{ACCOUNT}:role/voclabs'],
            eks_public_access_cidrs=[ip + '/32'], node_instance_type='t3.medium',
            node_scaling={'min': 2, 'desired': 2, 'max': 3}, nat_mode='single'), args.apply)
    elif args.step == 'database':
        terraform(DATABASE / 'infra', 'database', dict(shared, platform=outputs('platform')['platform'],
            instance_class='db.t4g.micro', allocated_storage_gib=20, max_allocated_storage_gib=30,
            backup_retention_days=1, multi_az=False, final_snapshot_identifier='oficina-academy-final'), args.apply)
    elif args.step == 'gateway':
        values = dict(shared, environment=args.environment)
        try:
            values['authentication'] = outputs('authentication-' + args.environment)['authentication']
        except SESSION.client('s3').exceptions.NoSuchKey:
            pass
        try:
            ready = bool(outputs('gateway-' + args.environment)['gateway'].get('jwt_authorizer_id'))
        except SESSION.client('s3').exceptions.NoSuchKey:
            ready = False
        if args.jwt_ready or ready:
            values['jwt_ready'] = True
            values['customer_backend'] = outputs('backend-' + args.environment)['customer_backend']
        terraform(PLATFORM / 'gateway', 'gateway-' + args.environment, values, args.apply)
    elif args.step == 'authentication':
        gateway = outputs('gateway-' + args.environment)['gateway']
        terraform(SERVERLESS / 'infra', 'authentication-' + args.environment, dict(shared,
            environment=args.environment, academy_role_arn=lab_role,
            platform=outputs('platform')['platform'], database=outputs('database')['database'],
            jwt_issuer=gateway['issuer'], api_gateway_execution_arn=gateway['execution_arn'],
            lambda_zip_path=str(WORK / 'authentication.zip')), args.apply)
    elif args.step == 'backend':
        terraform(PLATFORM / 'backend', 'backend-' + args.environment, dict(shared,
            environment=args.environment, platform=outputs('platform')['platform']), args.apply)
    elif args.step == 'api':
        database = outputs('database')
        terraform(ROOT / 'aws', 'api-' + args.environment, dict(shared, environment=args.environment,
            academy_role_arn=lab_role, platform=outputs('platform')['platform'], database=database['database'],
            runtime_secret_arns=database['runtime_secret_arns']), args.apply)


if __name__ == '__main__':
    main()
