# ADR 001 - Historico relacional de status da OS

Status: aceita nesta implementacao.

## Contexto

O Tech Challenge exige visibilidade do tempo por status. Somente o status atual
e os timestamps globais da OS nao descrevem os periodos intermediarios. A abertura
atual retorna Aguardando Aprovacao e deve ser preservada por decisao do projeto.

## Decisao

Armazenar periodos em tabela filha da OS, com ordem sequencial, datas UTC e FK.
Centralizar transicoes na entidade e persistir o agregado em transacao, usando
versao otimista contra atualizacoes simultaneas. Manter o status atual na OS para
as consultas operacionais existentes, atualizado junto com o historico.

Para dados legados, registrar apenas o estado conhecido com inicio nulo. Medir
somente periodos encerrados cujo inicio seja conhecido. Nao inventar diagnostico
no caminho de abertura que o pula. Preservar o PostgreSQL e sua integridade
referencial, sem adicionar outro banco apenas para essas metricas.

## Consequencias

As consultas poderao agregar tempos por status sem depender da retencao de logs.
O historico cresce a cada transicao; os indices apoiam a consulta por OS e status.
O legado nao oferece tempos retroativos completos. Aberturas atuais nao produzem
amostras de diagnostico. A demonstracao desse indicador exigira um fluxo acordado
que realmente percorra a etapa, sem apresentar dados artificiais como reais.

Esta tabela registra periodos de status, nao auditoria completa de autores,
tentativas, recusas ou notificacoes. Nao foi adotado event sourcing: reconstruir
todo o agregado por eventos seria uma mudanca maior que a necessidade atual.
