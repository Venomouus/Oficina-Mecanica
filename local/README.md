# Demonstracao do Tech Challenge sem AWS

Este ambiente executa API, autenticador CPF, PostgreSQL e observabilidade no Docker.
O Grafana LGTM reune OpenTelemetry Collector, Prometheus, Loki e Tempo. E uma
configuracao de desenvolvimento/demonstracao, nao uma implantacao de producao.

## Iniciar

Pre-requisitos: Docker Desktop iniciado com containers Linux, Docker Compose v2
com suporte a `dockerfile_inline`, Python 3 e os dois repositorios abaixo.
O primeiro download do LGTM e grande; aguarde a conclusao.

Estrutura esperada pelo Compose (a mesma usada neste computador):

```text
Fiap/
  Oficina-Mecanica/                  # este repositorio
  Oficina-Serverless/
    Oficina-serverless/              # autenticador existente
```

Se clonar em outro lugar, ajuste somente `services.auth.build.context` no
`compose.yml`. O contexto precisa apontar para a raiz do repositorio serverless.
Use a versao da master que contem `Oficina.Autenticacao.Local`.

No terminal da raiz de Oficina-Mecanica:

```powershell
docker compose -f local/compose.yml up -d --build
python local/demo.py
```

O script cria clientes ficticios, servicos e duas OS a cada execucao; o banco e
exclusivo da demonstracao. Nao modifica os containers do Compose antigo. JWTs
ficam somente na memoria do script. O resultado fica em `local/evidencias.local.json`
(ignorado pelo Git; copie apenas o relatorio sem segredos para acompanhar a entrega).

| Acesso | URL | Credenciais locais |
|---|---|---|
| Swagger API | http://127.0.0.1:18080/swagger | POST /api/auth/login: admin / Admin@123 |
| Swagger autenticacao CPF | http://127.0.0.1:15081/swagger | POST /auth/cpf: 12345678909 |
| Grafana | http://127.0.0.1:13000/d/oficina-local | admin / admin-local-demo |
| Alertas | http://127.0.0.1:13000/alerting/list | mesma conta Grafana |
| Readiness da API | http://127.0.0.1:18080/health | anonimo; verifica banco |
| Liveness da API | http://127.0.0.1:18080/health/live | anonimo; verifica processo |

Essas senhas sao exclusivas do laboratorio, publicadas intencionalmente. As portas
estao restritas a 127.0.0.1. Nao publique este Compose na internet.
A chave RSA e gerada no tmpfs do autenticador e nunca versionada; depois de recriar
o autenticador, obtenha um novo JWT. O issuer interno usa 127.0.0.1:5081 porque API
e autenticador compartilham o namespace de rede, preservando a restricao existente
de issuer HTTP somente em loopback/Development.

## O que demonstrar no Grafana

Execute o script e aguarde cerca de 30 segundos. O painel provisionado apresenta:

- OS criadas hoje e contagem por dia UTC nos ultimos sete dias.
- Latencia p95 e taxa de requisicoes por template de rota.
- Tempos medios e quantidade de periodos concluidos por status nas ultimas 24 horas.
- Erros HTTP 5xx e falhas nas rotas de OS.
- Memoria e CPU **do processo da API**; estes paineis nao medem o Kubernetes.
- Logs JSON da API. Em Explore selecione Tempo para pesquisar traces de `Oficina.API`.

Os valores de negocio sao consultados no PostgreSQL a cada 10 segundos e exportados
por OTLP. Os paineis de negocio usam janelas fixas (sete dias/24h); nao mudam a janela
da consulta SQL quando o seletor temporal do Grafana muda. Periodos abertos e datas
de inicio desconhecidas sao excluidos da media. `Finalizada` mede o intervalo entre
finalizar e entregar. Ausencia de amostras nao e tempo zero. Se a coleta falhar,
o ultimo valor e preservado e os indicadores de coleta/idade mostram a falha.

O fluxo do cliente continua iniciando em Aguardando Aprovacao. Para gerar Diagnostico
real, somente a abertura administrativa aceita `iniciarEmDiagnostico: true`, seguida
de envio para aprovacao. O script percorre ambos os caminhos sem inventar datas.

Logs proprios usam template de rota e trace_id, sem corpo, CPF, JWT ou senha.
O X-Correlation-ID da resposta corresponde ao trace ativo. Erros inesperados retornam
500 generico; o log proprio registra o tipo da excecao sem mensagem potencialmente
sensivel. A instrumentacao cobre a API, chamadas HTTP e atividades Npgsql; o host
local do autenticador mantem seus proprios logs JSON, sem exportacao OTLP nesta entrega.

## Falha controlada e alertas

```powershell
python local/demo.py --falha
```

O script interrompe **apenas** o banco deste Compose, provoca um 500 em OS, verifica
readiness 503/liveness 200 e a metrica de falha, e reinicia o banco em `finally`.
No Grafana, a regra "Falha no processamento de OS" dispara quando o contador aumenta
nos ultimos cinco minutos. Depois desse intervalo sem novos erros, volta ao normal.
A segunda regra detecta coleta indisponivel/desatualizada. Sao alertas visiveis no
Grafana; nao ha envio de e-mail/Slack configurado. Se interromper o Python abruptamente:

```powershell
docker compose -f local/compose.yml start db
```

## Parar e retomar

```powershell
docker compose -f local/compose.yml stop
docker compose -f local/compose.yml start
```

`stop` preserva volumes. Para diagnosticar falhas: `docker compose -f local/compose.yml ps`
e `docker compose -f local/compose.yml logs --tail 60`. Nunca compartilhe tokens de
Swagger ou conteudos de segredos. A autenticacao CPF consulta o cadastro ativo.

## Limites para a avaliacao

Nao demonstra API Gateway implantado, Lambda gerenciada, RDS gerenciado, EKS, HPA em
operacao, dashboards de CPU/memoria do cluster, deploy automatico cloud, notificacoes
serverless ou alta disponibilidade. O codigo Terraform e os manifests desses
componentes continuam nos quatro repositorios. CI/teste Kind nao equivale a CD cloud.
Veja [roteiro de entrega](../docs/entrega/README.md) e [arquitetura](../docs/entrega/arquitetura.md).

Referencia da ferramenta escolhida: https://github.com/grafana/docker-otel-lgtm/tree/v0.33.0
