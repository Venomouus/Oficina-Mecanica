"""kubectl exec credential provider; its stdout is consumed by kubectl, not logged."""
import base64
import datetime
import json
import os
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'local/.aws-sdk.local'))
import boto3
from botocore.signers import RequestSigner

session = boto3.Session(profile_name=os.getenv('AWS_PROFILE', 'academy'), region_name='us-east-1')
client = session.client('sts')
signer = RequestSigner(client.meta.service_model.service_id, 'us-east-1', 'sts', 'v4',
                       session.get_credentials(), session.events)
url = signer.generate_presigned_url({
    'method': 'GET', 'url': 'https://sts.us-east-1.amazonaws.com/?Action=GetCallerIdentity&Version=2011-06-15',
    'body': {}, 'headers': {'x-k8s-aws-id': sys.argv[1]}, 'context': {}},
    region_name='us-east-1', expires_in=60, operation_name='')
token = 'k8s-aws-v1.' + base64.urlsafe_b64encode(url.encode()).decode().rstrip('=')
expires = datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(minutes=10)
print(json.dumps({'apiVersion': 'client.authentication.k8s.io/v1beta1', 'kind': 'ExecCredential',
                  'status': {'expirationTimestamp': expires.strftime('%Y-%m-%dT%H:%M:%SZ'), 'token': token}}))
