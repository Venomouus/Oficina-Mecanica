"""CD for protected branches on the Academy Windows runner; no PR execution."""
import argparse
import os
import shutil
import subprocess
import tempfile
import time
from pathlib import Path
from provision import ROOT, WORK, SERVERLESS, command
from finish import run


def deploy(repo, env):
    WORK.mkdir(parents=True, exist_ok=True)
    if repo == 'Oficina-infra-kubernetes':
        run('provision.py', 'platform', '--apply')
        run('provision.py', 'backend', '--environment', env, '--apply')
        run('provision.py', 'gateway', '--environment', env, '--apply')
    elif repo == 'Oficina-infra-database':
        run('provision.py', 'database', '--apply')
    elif repo == 'Oficina-serverless':
        destination = WORK / 'auth-publish'
        command(['dotnet', 'publish', str(SERVERLESS / 'src/Oficina.Autenticacao/Oficina.Autenticacao.csproj'),
            '-c', 'Release', '-r', 'linux-x64', '--self-contained', 'false', '-o', str(destination)], 'auth-build')
        shutil.make_archive(str(WORK / 'authentication'), 'zip', str(destination))
        run('provision.py', 'authentication', '--environment', env, '--apply')
    elif repo == 'Oficina-Mecanica':
        command(['docker', 'build', '-t', 'oficina-academy-api:entrega', str(ROOT)], 'api-build')
        run('provision.py', 'api', '--environment', env, '--apply')
        run('application.py', 'publish', '--environment', env)
        run('application.py', 'deploy', '--environment', env)
    else:
        raise ValueError('Repositorio inesperado.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repository', required=True)
    parser.add_argument('--branch', choices=['develop', 'master'], required=True)
    args = parser.parse_args()
    # Four repository runners share one Academy account. Serialize deploys on this PC.
    import msvcrt
    lock = open(Path(tempfile.gettempdir()) / 'oficina-academy-cd.lock', 'a+b')
    lock.write(b'0'); lock.flush(); lock.seek(0)
    deadline = time.monotonic() + 1800
    while True:
        try:
            msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
            break
        except OSError:
            if time.monotonic() > deadline:
                raise RuntimeError('Outro deploy ainda esta em andamento.')
            time.sleep(5)
    try:
        deploy(args.repository, 'staging' if args.branch == 'develop' else 'producao')
    finally:
        lock.seek(0); msvcrt.locking(lock.fileno(), msvcrt.LK_UNLCK, 1); lock.close()
