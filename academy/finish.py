"""Continue the Academy deployment, stopping on the first error. Creates AWS resources."""
import subprocess
import sys
import urllib.request
from provision import ROOT, outputs


def run(script, *args):
    subprocess.run([sys.executable, str(ROOT / 'academy' / script), *args], check=True)


if __name__ == '__main__':
    run('provision.py', 'database', '--apply')
    run('kube.py', 'controller')
    run('monitoring.py')
    for env in ('staging', 'producao'):
        for root in ('gateway', 'authentication', 'api', 'backend'):
            run('provision.py', root, '--environment', env, '--apply')
        run('application.py', 'secrets', '--environment', env)
        run('provision.py', 'gateway', '--environment', env, '--apply')
        issuer = outputs('gateway-' + env)['gateway']['issuer']
        with urllib.request.urlopen(issuer + '/.well-known/jwks.json', timeout=30) as response:
            if response.status != 200:
                raise RuntimeError('JWKS ainda indisponivel.')
        run('application.py', 'publish', '--environment', env)
        run('application.py', 'deploy', '--environment', env)
        run('provision.py', 'gateway', '--environment', env, '--jwt-ready', '--apply')
        print(env + ' implantado: ' + issuer, flush=True)
    print('Implantacao concluida. Validar CPF/OS/dashboard antes do video; CD e acesso GitHub sao etapas separadas.')
