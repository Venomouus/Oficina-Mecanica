# Implantacao Academy

## Estado atual

Conexao STS confirmada; relogio corrigido. LabRole existe e sua trust policy inclui
EKS, EC2 e Lambda. Bucket de estado, EKS com dois workers, RDS, Gateways e backends
privados criados. API nos namespaces staging/producao com migrations separadas.
Fluxo real CPF/JWT/OS de homologacao registrado em `docs/entrega/evidencias-aws.json`.
O Academy nega a consulta S3 ObjectLock por SCP: o bucket de bootstrap foi mantido,
e Terraform gerencia seu bloqueio publico, criptografia e versionamento.

## Modo de laboratorio

Nos roots `infra/` de Kubernetes/serverless e `aws/` da API, a variavel opcional
`academy_role_arn` reutiliza uma role existente. Com null, permanece o modo IAM/IRSA
original. No Academy, nao sao criadas roles, policies ou provedor OIDC IAM por esses
roots. As permissoes efetivas de cada servico continuam limitadas pelo laboratorio.

EKS e workers usam a LabRole. O CNI usa a identidade do node. O Load Balancer
Controller usa hostNetwork para acessar a identidade do node com IMDSv2, sem aumentar
o hop limit para os pods comuns. Essa e uma concessao de laboratorio; em conta normal
usar IRSA e permissoes separadas por componente.

API e migrations nao recebem a identidade do node nem credenciais do Academy.
O operador executa `python academy/sync-secrets.py --config <contrato-gerado.json>`
para copiar os valores necessarios do Secrets Manager em Secrets distintos do
Kubernetes. `deploy/render.py` monta somente o segredo correspondente e a API
reutiliza as validacoes de usuario, finalidade e TLS. Rotacao exige sincronizar
novamente e reiniciar os pods. Os valores nao entram no Terraform ou Git.

## Ordem apos sincronizar o relogio

Na raiz da API, usar o perfil `academy` configurado em `%USERPROFILE%/.aws/credentials`.
`python academy/provision.py platform --apply` cria a plataforma.
Depois de sucesso, `python academy/finish.py` executa banco, controlador, monitoramento,
Lambda, Gateway e API nos dois ambientes. Para no primeiro erro; nao apaga recursos.
Os logs e contratos publicos ficam em `academy/.work.local/` (fora do Git).
Os logs de cada etapa permitem distinguir recursos criados de verificacoes pendentes.

Para o video, acessar Grafana por `kubectl -n observability port-forward service/lgtm 13000:3000`,
com o kubeconfig gerado em `.work.local/kubeconfig.local.json`. A senha fica no Secret
`grafana-admin`, nunca no README. O LGTM usa armazenamento temporario de demonstracao;
nao e uma instalacao de monitoramento duravel para producao.

1. Revisar/aplicar `academy/bootstrap`: bucket privado/versionado para states.
2. Usar esse backend S3 com chaves separadas por root e ambiente, `use_lockfile=true`.
3. Plataforma: LabRole, administrador IAM voclabs, IP publico administrativo /32,
   dois workers t3.medium e um NAT compartilhado; revisar o custo antes de apply.
4. RDS: um banco gerenciado compartilhado, dois bancos logicos; bootstrap e grants
   continuam pertencendo ao repositorio de banco.
5. Gateway inicial, Lambda, bootstrap do banco, API e integracao final conforme
   os contratos existentes. Configurar CD e monitoramento do cluster antes do video.

`preflight.py` apenas consulta servicos; nao aplica infraestrutura. A disponibilidade
de LabRole nao prova permissao para todas as operacoes. O workflow `academy-deploy.yml`
executa somente develop/master em runners Windows com label `academy`. Quatro runners
foram registrados com autorizacao do operador. O PC, Docker e a sessao Academy precisam
estar ativos; renovar as credenciais locais quando expirarem. Nenhuma access key vai ao GitHub.
O deploy so fica demonstrado pela pipeline depois de publicar/mesclar os PRs e obter
uma execucao verde. Notificacoes externas continuam pendentes.

## Monitoramento e video

O LGTM tem dashboard de negocio, logs e traces OTLP, metricas cAdvisor de CPU/memoria
de pods e sondas HTTP de healthcheck. As regras de alerta aparecem em Grafana > Alerting.
Nao configuramos destinatario externo de email. `python academy/demo.py` gera duas OS
de demonstracao e verifica o fluxo real sem imprimir JWTs ou senhas.

Os runners ficam em `%LOCALAPPDATA%/OficinaAcademyRunners`, um por repositorio.
Ao terminar o laboratorio, encerre os runners e remova-os em Settings > Actions > Runners.
Encerrar a sessao Academy nao deve ser usado como substituto da remocao dos recursos
provisionados. EKS, NAT, ALBs e RDS permanecem sujeitos ao consumo do saldo enquanto existirem.

## Horario do Windows

Se a sincronizacao pela interface falhar, abrir PowerShell **como administrador**:

```powershell
Start-Service w32time
w32tm /resync
```

Se retornar outra falha, registrar o erro. Nao substituir keys por causa de um erro
de horario; renovar somente quando a sessao/credenciais expirarem.
