# Bundle publico RDS

Origem: https://truststore.pki.rds.amazonaws.com/global/global-bundle.pem

Obtido em 2026-09-14, mesmo bundle revisado do Oficina-serverless.
SHA-256: `E5BB2084CCF45087BDA1C9BFFDEA0EB15EE67F0B91646106E466714F9DE3C7E3`.

Contem somente certificados publicos de CA. Incluido no publish para TLS VerifyFull
(cadeia e hostname); nao e chave privada JWT. Antes de rotacao/expiracao das CAs,
atualizar da origem oficial, revisar certificados e registrar o novo hash.
