"""Read-only Academy permissions discovery. Never prints credentials or secret values."""
import os
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'local/.aws-sdk.local'))
import boto3
from botocore.config import Config
from botocore.exceptions import ClientError

os.environ.setdefault('AWS_SHARED_CREDENTIALS_FILE', str(Path.home() / '.aws/credentials'))
session = boto3.Session(profile_name=os.getenv('AWS_PROFILE', 'academy'), region_name='us-east-1')
config = Config(connect_timeout=8, read_timeout=15, retries={'max_attempts': 1})


def main():
    tasks = [
        ('identity', 'sts', 'get_caller_identity', {}, lambda r: r['Account']),
        ('EKS', 'eks', 'list_clusters', {}, lambda r: r['clusters']),
        ('EKS versions', 'eks', 'describe_cluster_versions', {},
         lambda r: [v['clusterVersion'] for v in r['clusterVersions'] if v.get('status') == 'standard-support']),
        ('RDS', 'rds', 'describe_db_instances', {}, lambda r: [v['DBInstanceIdentifier'] for v in r['DBInstances']]),
        ('LabRole policies', 'iam', 'list_attached_role_policies', {'RoleName': 'LabRole'},
         lambda r: [p['PolicyName'] for p in r['AttachedPolicies']]),
        ('LabRole inline', 'iam', 'list_role_policies', {'RoleName': 'LabRole'}, lambda r: r['PolicyNames']),
        ('S3 state buckets', 's3', 'list_buckets', {}, lambda r: [b['Name'] for b in r['Buckets'] if 'oficina' in b['Name']]),
    ]
    for title, service, action, args, select in tasks:
        if len(sys.argv) > 1 and service != sys.argv[1]:
            continue
        try:
            result = getattr(session.client(service, config=config), action)(**args)
            print(title, select(result), flush=True)
        except ClientError as e:
            print(title, e.response['Error']['Code'], flush=True)
            if e.response['Error']['Code'] == 'InvalidSignatureException':
                print(e.response['Error']['Message'][:250], flush=True)
                print('AWS Date:', e.response['ResponseMetadata']['HTTPHeaders'].get('date'), flush=True)


if __name__ == '__main__':
    main()
