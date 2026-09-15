$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path $PSScriptRoot '.work.local'
$env:AWS_PROFILE = 'academy'
$env:AWS_SHARED_CREDENTIALS_FILE = Join-Path $env:USERPROFILE '.aws/credentials'
python (Join-Path $PSScriptRoot 'kube.py') connect
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$env:KUBECONFIG = Join-Path $work 'kubeconfig.local.json'
$kubectl = Join-Path $work 'kubectl.exe'
foreach ($forward in @(
    @{namespace='observability'; service='lgtm'; port=13000; mappings=@('13000:3000','19090:9090','13100:3100','13200:3200')},
    @{namespace='oficina-staging'; service='oficina-api'; port=18080; mappings=@('18080:8080')}
)) {
    if (-not (Test-NetConnection 127.0.0.1 -Port $forward.port -InformationLevel Quiet -WarningAction SilentlyContinue)) {
        Start-Process -FilePath $kubectl -ArgumentList (@('-n',$forward.namespace,'port-forward',('service/'+$forward.service)) + $forward.mappings) -WindowStyle Hidden
    }
}
$encoded = & $kubectl -n observability get secret grafana-admin -o 'jsonpath={.data.password}'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$password = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded))
Write-Host 'Grafana: http://127.0.0.1:13000 - usuario admin'
Write-Host "Senha Grafana (nao mostrar no video): $password"
Write-Host 'Demonstracao: python academy/demo.py'
Write-Host 'Postman: docs/entrega/Oficina-AWS.postman_collection.json'
Start-Process 'http://127.0.0.1:13000/d/oficina-local'
