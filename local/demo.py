"""Demonstracao local reproduzivel. Python 3, sem dependencias externas.
Nao imprime JWTs. Usa somente dados de demonstracao e portas locais.
"""
import argparse
import base64
import datetime as dt
import json
from pathlib import Path
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request

API = 'http://127.0.0.1:18080'
AUTH = 'http://127.0.0.1:15081'
ROOT = Path(__file__).resolve().parent.parent
CHECKS = []


def request(url, method='GET', body=None, token=None, expected=200):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(url, data=None if body is None else json.dumps(body).encode(),
                                 headers=headers, method=method)
    try:
        response = urllib.request.urlopen(req, timeout=30)
    except urllib.error.HTTPError as error:
        response = error
    raw = response.read()
    if response.status != expected:
        raise RuntimeError(f'{method} {urllib.parse.urlparse(url).path}: HTTP {response.status}, esperado {expected}')
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        return raw.decode()


def check(name):
    CHECKS.append(name)
    print('OK - ' + name, flush=True)


def wait_for(fn, timeout=120):
    end = time.monotonic() + timeout
    last = None
    while time.monotonic() < end:
        try:
            result = fn()
            if result:
                return result
        except (OSError, RuntimeError, KeyError) as error:
            last = type(error).__name__
        time.sleep(3)
    raise RuntimeError('Tempo esgotado aguardando servico/metrica: ' + str(last))


def prom(query):
    return request('http://127.0.0.1:19090/api/v1/query?' + urllib.parse.urlencode({'query': query}))['data']['result']


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--falha', action='store_true', help='Interrompe SOMENTE o banco da demonstracao, testa 500 e restaura-o')
    args = parser.parse_args()
    wait_for(lambda: request(API + '/health/live'))
    wait_for(lambda: request(AUTH + '/.well-known/openid-configuration'))
    admin = request(API + '/api/auth/login', 'POST', {'usuario': 'admin', 'senha': 'Admin@123'})['accessToken']
    customers = request(API + '/api/clientes', token=admin)
    cpfs = ['12345678909', '19401945802']
    for i, cpf in enumerate(cpfs):
        if not any(''.join(filter(str.isdigit, c['cpfCnpj'])) == cpf for c in customers):
            request(API + '/api/clientes', 'POST', {'nome': f'Cliente demonstracao {i+1}', 'cpfCnpj': cpf,
                    'telefone': '11999999999', 'email': f'cliente{i+1}@example.test'}, admin, 201)
    a, b = [request(AUTH + '/auth/cpf', 'POST', {'cpf': cpf})['accessToken'] for cpf in cpfs]
    check('Autenticacao CPF de dois clientes e emissao de JWT')
    service = request(API + '/api/servicos', 'POST', {'nome': 'Revisao demonstracao', 'descricao': 'Dados de teste',
        'preco': 120, 'tempoEstimadoMinutos': 45, 'ativo': True}, admin, 201)['id']
    payload = {'veiculo': {'placa': 'DEM1234', 'marca': 'Fiat', 'modelo': 'Uno', 'ano': 2020},
               'servicosIds': [service], 'pecas': [], 'observacoes': 'Demonstracao local'}
    order = request(API + '/api/minhas-ordens-servico', 'POST', payload, a, 201)
    assert order['status'] == 'Aguardando Aprovacao'
    oid = order['id']
    request(API + '/api/minhas-ordens-servico/' + oid, token=b, expected=404)
    request(API + '/api/clientes', token=a, expected=403)
    request(API + '/api/minhas-ordens-servico', expected=401)
    request(API + '/api/minhas-ordens-servico', 'POST', dict(payload, iniciarEmDiagnostico=True), a, 400)
    check('OS aguardando aprovacao; isolamento 404; perfil 403; sem token 401; campo administrativo rejeitado')
    approved = request(API + f'/api/minhas-ordens-servico/{oid}/aprovar', 'POST', token=a)
    assert approved['orcamentoAprovado'] is True
    for status in [5, 6]:
        time.sleep(2)
        request(API + f'/api/ordens-servico/{oid}/status', 'PATCH', {'status': status}, admin)
    check('Aprovacao, execucao, finalizacao e entrega da OS do cliente')
    diagnostic = request(API + '/api/ordens-servico', 'POST',
        dict(payload, cpfCnpjCliente=cpfs[0], iniciarEmDiagnostico=True), admin, 201)
    did = diagnostic['id']
    time.sleep(2)
    request(API + f'/api/ordens-servico/{did}/status', 'PATCH', {'status': 3}, admin)
    request(API + f'/api/minhas-ordens-servico/{did}/aprovar', 'POST', token=a)
    for status in [5, 6]:
        time.sleep(2)
        request(API + f'/api/ordens-servico/{did}/status', 'PATCH', {'status': status}, admin)
    check('Diagnostico administrativo e periodos reais por status, sem alterar datas')
    wait_for(lambda: prom('oficina_os_today') and float(prom('oficina_os_today')[0]['value'][1]) >= 2)
    periods = wait_for(lambda: prom('oficina_os_status_duration_seconds'))
    assert {'EmDiagnostico', 'EmExecucao', 'Finalizada'} <= {p['metric']['status'] for p in periods}
    wait_for(lambda: prom('oficina_os_daily'))
    check('Metricas persistidas: volume diario e tempos de Diagnostico, Execucao e Finalizacao')
    wait_for(lambda: request('http://127.0.0.1:13200/api/search')['traces'])
    wait_for(lambda: request('http://127.0.0.1:13100/loki/api/v1/query_range?' +
        urllib.parse.urlencode({'query': '{service_name="Oficina.API"}', 'limit': 5}))['data']['result'])
    check('Traces no Tempo e logs JSON correlacionados no Loki')
    if args.falha:
        compose = ['docker', 'compose', '-f', str(ROOT / 'local/compose.yml')]
        # Establish a zero baseline for the failure counter before interrupting the demo database.
        request(API + '/api/ordens-servico', token=admin)
        time.sleep(12)
        try:
            subprocess.run(compose + ['stop', 'db'], check=True, stdout=subprocess.DEVNULL)
            request(API + '/api/ordens-servico', token=admin, expected=500)
            request(API + '/health', expected=503)
            request(API + '/health/live')
            wait_for(lambda: prom('sum(increase(oficina_os_failures_total[5m])) > 0'))
            check('Falha real de banco: OS 500, readiness 503, liveness 200 e metrica de alerta')
        finally:
            subprocess.run(compose + ['start', 'db'], check=True, stdout=subprocess.DEVNULL)
        wait_for(lambda: request(API + '/health'))
        check('Banco da demonstracao restaurado e readiness recuperada')
    report = {'executadoEmUtc': dt.datetime.now(dt.timezone.utc).isoformat(), 'ambiente': 'Docker local, sem AWS',
              'verificacoes': CHECKS, 'falhaControlada': args.falha,
              'limites': ['Nao comprova deploy cloud', 'CPU/memoria sao do processo da API, nao do Kubernetes',
                         'Nao comprova notificacao serverless nem envio externo de alertas']}
    path = ROOT / 'local/evidencias.local.json'
    path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
    print('Evidencias: ' + str(path))
    print('Grafana: http://127.0.0.1:13000/d/oficina-local - admin / admin-local-demo')


if __name__ == '__main__':
    main()
