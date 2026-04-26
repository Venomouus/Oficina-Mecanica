# Relatorio de Vulnerabilidades

## Escopo

Analise estatica manual do MVP ASP.NET Core 8, considerando autenticacao, exposicao de dados sensiveis, validacao de entrada, configuracao e dependencias.

## Pontos positivos

- APIs administrativas protegidas por JWT.
- Swagger configurado com esquema Bearer.
- CPF/CNPJ validado e normalizado antes de gravacao.
- Placa validada nos formatos brasileiro antigo e Mercosul.
- Consulta e aprovacao de OS pelo cliente exigem documento vinculado.
- EF Core reduz risco de SQL injection por uso de LINQ parametrizado.
- Estoque possui regra de bloqueio para quantidade insuficiente.

## Vulnerabilidades e riscos identificados

| Severidade | Item | Risco | Recomendacao |
| --- | --- | --- | --- |
| Alta | Credenciais administrativas no `appsettings.json` | Vazamento de senha e chave JWT em repositorio | Usar variaveis de ambiente, user-secrets ou vault. |
| Media | JWT sem refresh token e sem revogacao | Token valido ate expirar mesmo apos comprometimento | Implementar expiracao curta, refresh token e blacklist quando necessario. |
| Media | Endpoints publicos usam CPF/CNPJ como fator unico | Documento pode ser conhecido por terceiros | Adicionar token de acompanhamento da OS ou autenticacao do cliente. |
| Media | `EnsureCreated` no startup | Pouco controle de evolucao de schema | Usar migrations EF Core em ambientes compartilhados/producao. |
| Baixa | Sem rate limit | Tentativas repetidas de login/consulta | Adicionar rate limiting no ASP.NET Core. |
| Baixa | Sem logs/auditoria de acoes administrativas | Dificulta rastrear alteracoes em OS/estoque | Registrar usuario, data e acao em trilha de auditoria. |

## Comando de verificacao executado

```powershell
dotnet test OficinaMecanica.sln --collect:"XPlat Code Coverage"
```

Resultado: 23 testes aprovados, 0 falhas, cobertura de linhas de 89,82%.

## Conclusao

O MVP atende aos requisitos basicos de seguranca para demonstracao academica, mas antes de producao deve remover segredos do codigo, fortalecer a autenticacao do cliente, adicionar rate limiting, auditoria e migrations controladas.
