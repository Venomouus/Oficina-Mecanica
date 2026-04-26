# Documentacao DDD

## Linguagem ubiqua

- Cliente: pessoa fisica ou juridica que solicita manutencao.
- Veiculo: automovel vinculado a um cliente, identificado por placa.
- Ordem de Servico (OS): registro central do atendimento, diagnostico, orcamento, execucao e entrega.
- Servico: mao de obra oferecida pela oficina, com preco e tempo estimado.
- Peca/Insumo: item consumido durante a execucao, com controle de estoque.
- Orcamento: valor calculado por servicos mais pecas/insumos.
- Aprovacao: aceite do cliente para liberar a execucao.

## Bounded contexts

- Atendimento: identifica cliente, cadastra veiculo e abre OS.
- Execucao de Servicos: controla status, execucao, finalizacao e entrega.
- Estoque: controla pecas/insumos e baixa durante a execucao.
- Administrativo: mantem cadastros, metricas e operacoes internas protegidas por JWT.

## Event Storming - criacao e acompanhamento da OS

1. Cliente identificado por CPF/CNPJ.
2. Veiculo cadastrado ou localizado por placa.
3. Servicos solicitados adicionados.
4. Pecas e insumos previstos adicionados.
5. Orcamento calculado automaticamente.
6. OS criada com status `AguardandoAprovacao`.
7. Cliente consulta OS pela API.
8. Cliente aprova orcamento.
9. Estoque de pecas/insumos e baixado.
10. OS entra em `EmExecucao`.
11. Oficina finaliza o servico.
12. OS entra em `Finalizada`.
13. Veiculo e entregue.
14. OS entra em `Entregue`.

## Event Storming - gestao de pecas e insumos

1. Administrador cadastra peca/insumo.
2. Administrador ajusta estoque.
3. OS reserva itens no orcamento.
4. Aprovacao/execucao baixa estoque.
5. Estoque insuficiente bloqueia a execucao.
6. Administrador monitora itens abaixo do estoque minimo.

## Agregados

- `OrdemServico`: agregado principal, controla status, orcamento, itens de servico e itens de peca.
- `PecaInsumo`: controla estoque e regras de baixa/reposicao.
- `Cliente` e `Veiculo`: mantem dados cadastrais e relacao de propriedade.

## Regras de dominio

- CPF/CNPJ deve ser valido para cadastro e consulta.
- Placa deve seguir formato antigo ou Mercosul.
- OS criada com orcamento fica aguardando aprovacao.
- OS so pode ser aprovada quando estiver aguardando aprovacao.
- OS so pode ser finalizada quando estiver em execucao.
- OS so pode ser entregue quando estiver finalizada.
- Estoque nao pode ficar negativo.
