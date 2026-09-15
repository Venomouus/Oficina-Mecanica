# Conectar ao Academy: caminho minimo

## Revisao do codigo em 2026-09-14

A aplicacao e a demonstracao local funcionam. O projeto nao esta pronto para apenas
informar credenciais e aplicar toda a infraestrutura no Academy.

| Ponto | Situacao encontrada | Proxima decisao |
|---|---|---|
| IAM | Plataforma, controller, API e Lambda criam roles/policies proprias | Conferir restricoes e trust policies das roles existentes no lab |
| EKS | Endpoint privado por padrao; acesso publico limitado por CIDRs e configuravel | Escolher acesso operacional permitido antes de aplicar |
| RDS | Classes aceitas pelo codigo: db.t4g.micro/small/medium; senha master gerenciada | Conferir classes e Secrets Manager permitidos no lab |
| Autenticacao | Lambda .NET 8, JWT RSA e acesso privado ao banco preparados | Preencher outputs reais, segredos e configurar Gateway |
| CD | CI e teste Kind existentes; nao ha CD AWS completo nos quatro repos | Configurar deploy hom/prod depois da identidade e acesso operacional |
| Observabilidade | Grafana/OTLP local funcionando | Conectar telemetria cloud e acrescentar metricas reais de pods/nodes |
| Notificacoes | Contrato/projeto parcial | Concluir o caminho minimo de evento e envio demonstravel |

Nao trocar todas as roles por LabRole automaticamente: a trust policy deve aceitar
o servico que a usara. Uma role para Lambda/EC2 nao e automaticamente uma role IRSA.
Uma chamada Describe/List bem-sucedida tambem nao comprova permissao para criar.

## Primeiro passo no laboratorio

Quando a sessao estiver pronta, abra o console pelo link AWS do laboratorio. No
Readme do lab consulte `Service usage and other restrictions`, especialmente EKS,
IAM, RDS e regioes permitidas. Essas regras especificas prevalecem sobre exemplos
genericos da internet.

No terminal do proprio lab (ou CloudShell, se permitido), execute somente leituras:

```bash
aws sts get-caller-identity --query Arn --output text
aws configure get region
aws iam get-role --role-name LabRole --query 'Role.{Arn:Arn,Trust:AssumeRolePolicyDocument}' --output json
aws eks list-clusters --region us-east-1 --output json
```

Use a regiao permitida pelo seu Readme se diferente de us-east-1. Envie apenas os
resultados desses comandos e a restricao de EKS/IAM. Nao envie access key, secret key,
session token nem o conteudo de AWS Details. Se LabRole nao existir ou GetRole for
negado, isso e informacao para adaptar o caminho, nao motivo para tentar criar roles.

## Ordem de trabalho apos confirmar permissoes

1. Adaptar somente identidades e parametros incompativeis com o laboratorio.
2. Criar rede/cluster e banco; usar outputs reais para configurar os demais roots.
3. Preparar banco/segredos e aplicar migrations; publicar a API e autenticacao.
4. Conectar Gateway e validar uma autenticacao CPF e uma OS pelo endpoint publicado.
5. Conectar observabilidade, demonstrar alerta e recursos Kubernetes; concluir CD
   hom/prod e notificacoes para nao declara-los atendidos apenas pelo deploy manual.
6. Atualizar o estado real no PDF e gravar o video. Nao repetir a suite inteira:
   usar uma verificacao por integracao alterada e a demonstracao final.

Manter os quatro repositorios e o codigo funcional. Nao reconstruir a aplicacao nem
introduzir ferramentas novas sem necessidade. A possibilidade de concluir todo o
enunciado depende das permissoes reais do lab; nao esta confirmada pelo console aberto.

O orcamento exibido no lab e limitado. End Lab nao deve ser tratado como remocao de
todos os recursos. Acompanhar o saldo e planejar a remocao dos recursos criados apos
a demonstracao, preservando evidencias e respeitando o periodo de avaliacao.

Referencias: [AWS Academy](https://aws.amazon.com/training/awsacademy/) e
[permissoes IAM](https://docs.aws.amazon.com/IAM/latest/UserGuide/access_permissions-required.html).
