# ADR 002 - Autorizacao de clientes e administradores

Status: aceita nesta implementacao.

## Contexto

O serverless emite JWT RS256 para clientes ativos. A API existente utiliza JWT
HS256 para administradores e permitia consulta/aprovacao de OS mediante CPF na
URL. CPF e um identificador conhecido por terceiros; nao pode conceder acesso
a qualquer OS apenas por corresponder ao cadastro.

## Decisao

Manter validadores separados para os dois emissores. O middleware seleciona o
validador pelo issuer, mas somente concede identidade apos verificar assinatura,
algoritmo permitido, emissor, audiencia, validade e claims. O validador de cliente
aceita RS256 e tipo `at+jwt`; exige `sub` UUID, `role=Cliente` e scope
`oficina:cliente`. A chave publica vem do discovery/JWKS do serverless. A chave
privada permanece exclusivamente no emissor.

Politicas explicitas distinguem administrador de cliente, inclusive pelo esquema
que validou a identidade. Um JWT de cliente com papel Admin e rejeitado.
Consultar/alterar uma OS como cliente exige correspondencia entre `ClienteId` da
OS e `sub` do JWT. OS de terceiros e OS inexistente retornam o mesmo 404.
Parametros de CPF nao participam mais dessa autorizacao.

Oferecer `api/minhas-ordens-servico` para listar, abrir, consultar e aprovar OS.
A criacao recebe veiculo, servicos, pecas e observacoes; identidade vem do JWT.
Campos desconhecidos no corpo da criacao sao rejeitados. Uma placa ja pertencente
a outro cliente nao pode ser utilizada. A abertura continua em Aguardando Aprovacao.

Consultar o status ativo no banco a cada autenticacao de cliente. Desativar o
cadastro bloqueia tambem tokens emitidos anteriormente. Falhas de conexao/timeout
durante essa verificacao negam acesso com 503, sem liberar a identidade.

## Consequencias

Consumidores das antigas consultas por CPF precisam migrar para Bearer JWT.
O administrador continua autenticando no login existente e pode operar todas
as OS, mas nao assume a identidade de cliente nos endpoints pessoais.
A consulta de status ativo acrescenta uma leitura no PostgreSQL por requisicao.
Essa carga devera ser medida na etapa de observabilidade.

O middleware mantem cache dos metadados e das chaves publicas. A rotacao deve
preservar chaves antigas pelo periodo de validade dos tokens e caches; a operacao
de rotacao em nuvem sera tratada na etapa AWS. HTTP para discovery e aceito apenas
em loopback com ambiente Development/Testing; producao exige HTTPS.

O webhook de orcamento permanece como integracao administrativa separada com
`X-Webhook-Token`. Essa credencial permite registrar decisoes de clientes e deve
ser armazenada como segredo no deploy, sem ser entregue a clientes.

Autenticar somente com CPF atende ao fluxo solicitado no desafio, mas nao
comprova posse de uma credencial pessoal. Um uso real exige uma etapa adicional,
como codigo de uso unico, alem de limites de tentativas no Gateway. Essas evolucoes
nao estao implementadas neste PR.

Referencia: [politicas e selecao de esquemas no ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/policyschemes?view=aspnetcore-8.0).
