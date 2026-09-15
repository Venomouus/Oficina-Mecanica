"""Gera PDF com links clicaveis usando apenas Python 3 (sem instalar pacotes)."""
import json
from pathlib import Path
import textwrap

HERE = Path(__file__).resolve().parent


def literal(value):
    return value.encode('cp1252', errors='replace').replace(b'\\', b'\\\\').replace(b'(', b'\\(').replace(b')', b'\\)')


def main():
    data = json.loads((HERE / 'dados-entrega.json').read_text(encoding='utf-8'))
    lines = []

    def paragraph(text, size=11, url=None):
        for line in textwrap.wrap(text, width=86 if size == 11 else 64, break_long_words=True, break_on_hyphens=False):
            lines.append((line, size, url))
        lines.append(('', 11, None))

    paragraph(data['titulo'], 18)
    paragraph('Entrega: AWS Academy e alternativa local', 14)
    paragraph('EKS, RDS, Lambda e API Gateway provisionados. O fluxo CPF/JWT/OS foi demonstrado em homologacao, com metricas Kubernetes e traces. Consultar evidencias-aws.json e os limites da entrega.')
    if not data['video'].startswith('https://') or data['acesso_avaliador'].startswith('NAO CONFIRMADO'):
        paragraph('RASCUNHO - preencher video e confirmacao do avaliador antes de enviar.', 14)
    paragraph('1. Repositorios', 14)
    for url in data['repositorios']:
        paragraph(url, url=url)
    paragraph('2. Video de demonstracao (ate 15 minutos)', 14)
    paragraph(data['video'], url=data['video'] if data['video'].startswith('https://') else None)
    paragraph('3. Acesso do avaliador soat-architecture', 14)
    paragraph(data['acesso_avaliador'])
    paragraph('4. Documentacao', 14)
    paragraph('Os links da master abaixo passam a incluir os novos documentos depois da promocao do PR. Antes de enviar, abrir cada link e conferir seu conteudo.')
    for url in data['documentacao']:
        paragraph(url, url=url)
    paragraph('Endpoints AWS (homologacao e producao; rotas de cliente exigem JWT)', 14)
    for url in data.get('deploys', []):
        paragraph(url, url=url)
    paragraph('5. Funcionalidades demonstradas', 14)
    for item in [
        'Autenticacao por CPF: consulta de cliente existente/ativo, JWT RSA e consumo das APIs protegidas.',
        'Ordens de servico: abertura, aprovacao, execucao, finalizacao e entrega; isolamento por cliente com retorno 404 e separacao de acesso administrativo.',
        'Observabilidade com OpenTelemetry, Grafana, Prometheus, Loki e Tempo: latencia, volume diario de OS, medias de periodos reais por status, erros, logs correlacionados e traces da API.',
        'Regras de alerta provisionadas para falhas de OS e coleta indisponivel. Healthchecks separados de processo e banco.',
        'Quatro repositorios, workflows CI, Terraform, manifests Kubernetes e separacao entre migracoes e runtime da API.',
        'Diagramas de componentes, sequencia, modelo relacional e decisoes arquiteturais documentadas.'
    ]:
        paragraph('- ' + item)
    paragraph('6. Limites e requisitos ainda pendentes', 14)
    for item in [
        'Workflows CD e runners Academy configurados; conferir execucoes verdes das branches develop/master apos a promocao dos PRs.',
        'Um EKS e um RDS compartilhados por ambientes separados. RDS Single-AZ e monitoramento com armazenamento temporario: concessoes para economia no laboratorio.',
        'Sondas HTTP de uptime e metricas de pods estao no EKS. Alertas nao incluem envio externo de e-mail/Slack.',
        'Notificacoes serverless ponta a ponta permanecem pendentes. O autenticador local possui logs proprios, sem exportacao OTLP integrada.',
        'Master protegida e acesso de soat-architecture confirmados nos quatro repositorios pela API GitHub.',
        'Este pacote nao garante atendimento integral ao enunciado nem uma nota especifica.'
    ]:
        paragraph('- ' + item)
    paragraph('7. Executar a demonstracao', 14)
    paragraph('AWS: consultar academy/README.md e executar python academy/demo.py com os port-forwards ativos. Alternativa local: local/README.md.')
    paragraph('docker compose -f local/compose.yml up -d --build')
    paragraph('python local/demo.py')
    paragraph('Grafana: http://127.0.0.1:13000/d/oficina-local (admin / admin-local-demo). Swagger API: http://127.0.0.1:18080/swagger. Essas URLs so funcionam na maquina que executa o Compose, nao sao deploys publicos.')
    paragraph('Credenciais e dados publicados no Compose sao exclusivos da demonstracao local. Nao publicar o ambiente diretamente na internet.')

    pages, current, y = [], [], 790
    for text, size, url in lines:
        if y < 58:
            pages.append(current)
            current, y = [], 790
        current.append((text, size, url, y))
        y -= max(size + 5, 16)
    if current:
        pages.append(current)

    objects = [b'', b'', b'<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>']
    page_ids = []
    for index, page in enumerate(pages, 1):
        commands, annotations = [], []
        for text, size, url, y in page:
            commands.append(b'BT /F1 ' + str(size).encode() + b' Tf 48 ' + str(y).encode() + b' Td (' + literal(text) + b') Tj ET')
            if url:
                objects.append(b'<< /Type /Annot /Subtype /Link /Rect [48 ' + str(y-3).encode() + b' 548 ' + str(y+size).encode() + b'] /Border [0 0 0] /A << /S /URI /URI (' + literal(url) + b') >> >>')
                annotations.append(len(objects))
        commands.append(b'BT /F1 9 Tf 48 30 Td (Tech Challenge - ' + str(index).encode() + b'/' + str(len(pages)).encode() + b') Tj ET')
        stream = b'\n'.join(commands)
        objects.append(b'<< /Length ' + str(len(stream)).encode() + b' >>\nstream\n' + stream + b'\nendstream')
        content_id = len(objects)
        ann = ' '.join(f'{i} 0 R' for i in annotations)
        objects.append(f'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {content_id} 0 R /Annots [{ann}] >>'.encode())
        page_ids.append(len(objects))
    objects[0] = b'<< /Type /Catalog /Pages 2 0 R >>'
    objects[1] = ('<< /Type /Pages /Count ' + str(len(pages)) + ' /Kids [' + ' '.join(f'{i} 0 R' for i in page_ids) + '] >>').encode()
    output, offsets = bytearray(b'%PDF-1.4\n%\xe2\xe3\xcf\xd3\n'), [0]
    for i, obj in enumerate(objects, 1):
        offsets.append(len(output))
        output.extend(f'{i} 0 obj\n'.encode() + obj + b'\nendobj\n')
    xref = len(output)
    output.extend(f'xref\n0 {len(offsets)}\n0000000000 65535 f \n'.encode())
    for offset in offsets[1:]:
        output.extend(f'{offset:010d} 00000 n \n'.encode())
    output.extend(f'trailer\n<< /Size {len(offsets)} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n'.encode())
    path = HERE / 'Tech-Challenge-Oficina.pdf'
    path.write_bytes(output)
    print(f'PDF gerado: {path} ({len(pages)} paginas). Confira video e acesso em dados-entrega.json.')


if __name__ == '__main__':
    main()
