"""Short real AWS demonstration; never prints passwords or JWTs."""
import argparse
import datetime
import json
import sys
import time
from provision import ROOT, SESSION, outputs
sys.path.insert(0, str(ROOT / 'local'))
from demo import request, prom, wait_for


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--environment', choices=['staging', 'producao'], default='staging')
    parser.add_argument('--admin-url', default='http://127.0.0.1:18080')
    args = parser.parse_args()
    api = args.admin_url
    gateway = outputs('gateway-' + args.environment)['gateway']['issuer']
    cfg = outputs('api-' + args.environment)['api']
    admin_config = json.loads(SESSION.client('secretsmanager').get_secret_value(SecretId=cfg['secret_arns']['api'])['SecretString'])
    admin = request(api + '/api/auth/login', 'POST', {'usuario': admin_config['adminUser'], 'senha': admin_config['adminPassword']})['accessToken']
    cpf = '12345678909'
    if not any(''.join(filter(str.isdigit, c['cpfCnpj'])) == cpf for c in request(api + '/api/clientes', token=admin)):
        request(api + '/api/clientes', 'POST', {'nome': 'Cliente demonstracao AWS', 'cpfCnpj': cpf,
            'telefone': '11999999999', 'email': 'demo@example.test'}, admin, 201)
    token = request(gateway + '/auth/cpf', 'POST', {'cpf': cpf})['accessToken']
    print('OK - CPF consultado no RDS; JWT emitido pela Lambda.', flush=True)
    request(gateway + '/api/minhas-ordens-servico', expected=401)
    service = request(api + '/api/servicos', 'POST', {'nome': 'Revisao Academy', 'descricao': 'Demonstracao',
        'preco': 120, 'tempoEstimadoMinutos': 45, 'ativo': True}, admin, 201)['id']
    payload = {'veiculo': {'placa': 'DEM1234', 'marca': 'Fiat', 'modelo': 'Uno', 'ano': 2020},
               'servicosIds': [service], 'pecas': [], 'observacoes': 'Demonstracao AWS'}
    orders = []
    for diagnostic in (False, True):
        if diagnostic:
            order = request(api + '/api/ordens-servico', 'POST', dict(payload, cpfCnpjCliente=cpf, iniciarEmDiagnostico=True), admin, 201)
            time.sleep(2)
            request(api + '/api/ordens-servico/' + order['id'] + '/status', 'PATCH', {'status': 3}, admin)
        else:
            order = request(gateway + '/api/minhas-ordens-servico', 'POST', payload, token, 201)
        oid = order['id']; orders.append(oid)
        request(gateway + '/api/minhas-ordens-servico/' + oid + '/aprovar', 'POST', token=token)
        for status in (5, 6):
            time.sleep(2)
            request(api + '/api/ordens-servico/' + oid + '/status', 'PATCH', {'status': status}, admin)
    print('OK - Gateway protegido, abertura/aprovacao de OS e periodos reais por status.', flush=True)
    request(api + '/health')
    wait_for(lambda: prom('oficina_os_today'))
    wait_for(lambda: prom('container_memory_working_set_bytes{namespace="oficina-' + args.environment + '",container="oficina-api"}'))
    wait_for(lambda: request('http://127.0.0.1:13200/api/search')['traces'])
    report = {'executadoEmUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'ambiente': args.environment,
        'gateway': gateway, 'ordens': orders, 'verificacoes': ['Lambda CPF/RDS/JWT', 'Gateway sem token retorna 401',
        'OS criada e aprovada pelo Gateway', 'Diagnostico/Execucao/Finalizacao', 'Healthcheck RDS/API',
        'Metricas de negocio', 'Memoria de pods Kubernetes', 'Traces no Tempo']}
    (ROOT / 'docs/entrega/evidencias-aws.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('OK - Metricas e traces reais. Evidencias em docs/entrega/evidencias-aws.json.', flush=True)


if __name__ == '__main__':
    main()
