"""Copy only app/migration secrets from Secrets Manager into the selected EKS namespace.
No Academy access keys are copied. Requires deployment identity and kubectl access.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'local/.aws-sdk.local'))
import boto3


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, required=True)
    args = parser.parse_args()
    api = json.loads(args.config.read_text(encoding='utf-8'))['api']
    if not api.get('academy_mode') or api['environment'] not in ('staging', 'producao'):
        raise RuntimeError('Contrato Academy e ambiente valido obrigatorios.')
    session = boto3.Session(profile_name=os.getenv('AWS_PROFILE', 'academy'), region_name=api['aws_region'])
    if session.client('sts').get_caller_identity()['Account'] != api['aws_account_id']:
        raise RuntimeError('Conta AWS diferente do contrato.')
    context = subprocess.check_output(['kubectl', 'config', 'current-context'], text=True).strip()
    expected = f"arn:aws:eks:{api['aws_region']}:{api['aws_account_id']}:cluster/{api['cluster_name']}"
    if context != expected:
        raise RuntimeError('Selecione no kubectl o contexto ARN exato do cluster do contrato.')
    if api['namespace'] != 'oficina-' + api['environment']:
        raise RuntimeError('Namespace diferente do ambiente.')
    client = session.client('secretsmanager')
    for purpose, name in [('app', 'oficina-api'), ('migrations', 'oficina-migrations')]:
        data = {'database.json': client.get_secret_value(SecretId=api['secret_arns'][purpose])['SecretString']}
        if purpose == 'app':
            data['api.json'] = client.get_secret_value(SecretId=api['secret_arns']['api'])['SecretString']
        resource = {'apiVersion': 'v1', 'kind': 'Secret', 'type': 'Opaque',
                    'metadata': {'name': name, 'namespace': api['namespace']}, 'stringData': data}
        result = subprocess.run(['kubectl', 'apply', '--server-side', '--field-manager=oficina-secrets', '-f', '-'],
                                input=json.dumps(resource), text=True, capture_output=True)
        if result.returncode:
            raise RuntimeError('Falha ao sincronizar Secret; confira acesso ao namespace. Conteudo omitido.')
        print(f"Secret {api['namespace']}/{name} sincronizado (valores omitidos).")


if __name__ == '__main__':
    main()
