param([switch]$FromClipboard)

$ErrorActionPreference = 'Stop'

function Read-PrivateValue([string]$prompt) {
    $secure = Read-Host -Prompt $prompt -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
        $secure.Dispose()
    }
}

function Read-ClipboardPrivateValue([string]$label) {
    $value = Get-Clipboard -Raw
    Set-Clipboard -Value ' '
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "A area de transferencia estava vazia ao ler $label. Copie o valor no painel e tente novamente."
    }
    return $value
}

$context = & kubectl config current-context
if ($LASTEXITCODE -ne 0 -or $context -notmatch 'cluster/oficina-fase4-lab$') {
    throw 'Contexto Kubernetes incorreto. Selecione primeiro o cluster oficina-fase4-lab.'
}

try {
    $tunnels = Invoke-RestMethod -Uri 'http://127.0.0.1:4040/api/tunnels' -TimeoutSec 5
}
catch {
    throw 'ngrok nao encontrado. Mantenha ngrok http http://127.0.0.1:8082 aberto em outro terminal.'
}
$tunnel = $tunnels.tunnels | Where-Object {
    $_.public_url -like 'https://*' -and $_.config.addr -eq 'http://127.0.0.1:8082'
} | Select-Object -First 1
if (-not $tunnel) {
    throw 'Nao ha tunel HTTPS do ngrok apontando para http://127.0.0.1:8082.'
}
$webhookUrl = $tunnel.public_url.TrimEnd('/') + '/api/payments/webhook'
Write-Host "Webhook HTTPS: $webhookUrl"

$token = if ($FromClipboard) {
    Write-Host 'Lendo o Access Token da area de transferencia e limpando-a em seguida.'
    Read-ClipboardPrivateValue 'o Access Token'
} else {
    Read-PrivateValue 'Access Token de TESTE do Mercado Pago (nao sera exibido)'
}
$token = $token.Trim().Trim([char]0xFEFF).Trim()
if ($token -match '^Bearer\s+') { $token = $token -replace '^Bearer\s+', '' }
$token = $token.Trim([char[]]@([char]34, [char]39)).Trim()
if ([string]::IsNullOrWhiteSpace($token)) {
    throw 'Nenhum Access Token foi recebido. Copie o valor inteiro antes de continuar.'
}
if ($token -notmatch '^APP_USR') {
    throw 'O valor recebido nao e um Access Token de teste do Checkout Pro. No Mercado Pago Developers, abra a aplicacao > Teste > Credenciais e copie ACCESS TOKEN (nao Public Key, senha da conta de teste nem assinatura do webhook). Ele comeca com APP_USR. Se colar no terminal falhar, use -FromClipboard.'
}
$signature = if ($FromClipboard) {
    Read-Host 'Agora copie a assinatura secreta do Webhook no painel e pressione Enter aqui (nao cole no terminal)' | Out-Null
    Read-ClipboardPrivateValue 'a assinatura do Webhook'
} else {
    Read-PrivateValue 'Assinatura secreta do Webhook de TESTE (nao sera exibida)'
}
$signature = $signature.Trim()
if ([string]::IsNullOrWhiteSpace($signature)) {
    throw 'A assinatura secreta do Webhook e obrigatoria para proteger o endpoint publico.'
}

$secretJson = & kubectl -n oficina get secret mercado-pago-secrets -o json
if ($LASTEXITCODE -ne 0) { throw 'Secret mercado-pago-secrets nao encontrado.' }
$secret = $secretJson | ConvertFrom-Json
$data = @{}
foreach ($property in $secret.data.PSObject.Properties) {
    $data[$property.Name] = $property.Value
}
$utf8 = [Text.Encoding]::UTF8
$data['access-token'] = [Convert]::ToBase64String($utf8.GetBytes($token))
$data['webhook-url'] = [Convert]::ToBase64String($utf8.GetBytes($webhookUrl))
$data['webhook-secret'] = [Convert]::ToBase64String($utf8.GetBytes($signature))

$manifest = @{
    apiVersion = 'v1'
    kind = 'Secret'
    metadata = @{
        name = 'mercado-pago-secrets'
        namespace = 'oficina'
        resourceVersion = $secret.metadata.resourceVersion
    }
    type = 'Opaque'
    data = $data
} | ConvertTo-Json -Depth 5 -Compress
$manifest | & kubectl replace -f -
if ($LASTEXITCODE -ne 0) { throw 'Falha ao atualizar o Secret. O Billing nao foi reiniciado.' }

$token = $null
$signature = $null
$manifest = $null
$secretJson = $null

& kubectl -n oficina rollout restart deployment/billing-service
if ($LASTEXITCODE -ne 0) { throw 'Secret atualizado, mas o reinicio do Billing falhou.' }
& kubectl -n oficina rollout status deployment/billing-service --timeout=180s
if ($LASTEXITCODE -ne 0) { throw 'Secret atualizado, mas o Billing nao ficou pronto. Verifique os pods.' }
Write-Host 'Billing pronto. Teste uma OS nova, aprove o orcamento e crie a preferencia de pagamento.'
