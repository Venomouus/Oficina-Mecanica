$ErrorActionPreference = 'Stop'

function New-LabPassword {
    $bytes = New-Object byte[] 24
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    return [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
}

function Get-Secret([string]$name) {
    $json = & kubectl -n oficina get secrets -o json
    if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel consultar os Secrets do namespace oficina' }
    return ($json | ConvertFrom-Json).items | Where-Object { $_.metadata.name -eq $name } | Select-Object -First 1
}

function Set-Secret([string]$name, [hashtable]$values) {
    $arguments = @('-n', 'oficina', 'create', 'secret', 'generic', $name)
    foreach ($key in $values.Keys) {
        $arguments += "--from-literal=$key=$($values[$key])"
    }
    $yaml = & kubectl @arguments '--dry-run=client' '-o=yaml'
    if ($LASTEXITCODE -ne 0) { throw "Nao foi possivel gerar o Secret $name" }
    $null = $yaml | & kubectl apply -f -
    if ($LASTEXITCODE -ne 0) { throw "Nao foi possivel criar o Secret $name" }
}

function Ensure-Secret([string]$name, [hashtable]$values) {
    if (Get-Secret $name) { return }
    Set-Secret $name $values
}

function Get-DatabasePassword([string]$name) {
    $secret = Get-Secret $name
    if ($secret) {
        return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($secret.data.password))
    }
    $password = New-LabPassword
    Ensure-Secret $name @{ password = $password }
    return $password
}

& kubectl apply -f (Join-Path $PSScriptRoot '..\namespace.yaml')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar namespace oficina' }

$osPassword = Get-DatabasePassword 'os-db-credentials'
$billingPassword = Get-DatabasePassword 'billing-db-credentials'
$executionPassword = Get-DatabasePassword 'execution-db-credentials'

Set-Secret 'os-service-secrets' @{
    'database-connection' = "Host=os-db;Port=5432;Database=oficina_os;Username=oficina;Password=$osPassword"
}
Set-Secret 'billing-service-secrets' @{
    'database-connection' = "mongodb://oficina:${billingPassword}@billing-db:27017/?authSource=admin"
}
Set-Secret 'execution-service-secrets' @{
    'database-connection' = "mongodb://oficina:${executionPassword}@execution-db:27017/?authSource=admin"
}
Ensure-Secret 'messaging-secrets' @{
    username = 'oficina'
    password = (New-LabPassword)
}
# O pagamento real permanece desativado no lab ate configurar HTTPS publico e
# credenciais de sandbox. Valores vazios nao sao credenciais reais.
Ensure-Secret 'mercado-pago-secrets' @{
    'access-token' = ''
    'external-pos-id' = ''
    'webhook-url' = ''
    'webhook-secret' = ''
}
Ensure-Secret 'grafana-credentials' @{
    'admin-password' = (New-LabPassword)
}

& kubectl apply -f (Join-Path $PSScriptRoot 'infra-lab.yaml')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao aplicar infraestrutura do laboratorio' }
