$ErrorActionPreference = 'Stop'

function Wait-For([string]$url, [scriptblock]$predicate) {
    $deadline = (Get-Date).AddSeconds(60)
    do {
        try {
            $result = Invoke-RestMethod -Uri $url -TimeoutSec 5
            if (& $predicate $result) { return $result }
        } catch {
            # Pods podem demorar alguns segundos para ficar prontos.
        }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    throw "Tempo esgotado aguardando $url"
}

$kubectl = (Get-Command kubectl).Source
$forwards = @()
try {
    foreach ($entry in @(@('os-service', '28081:8081'), @('billing-service', '28082:8082'), @('execution-service', '28083:8083'))) {
        $forwards += Start-Process -FilePath $kubectl -ArgumentList @('-n', 'oficina', 'port-forward', "svc/$($entry[0])", $entry[1]) -WindowStyle Hidden -PassThru
    }
    Wait-For 'http://localhost:28081/health' { $true } | Out-Null
    Wait-For 'http://localhost:28082/health' { $true } | Out-Null
    Wait-For 'http://localhost:28083/health' { $true } | Out-Null

    $order = Invoke-RestMethod -Method Post -Uri 'http://localhost:28081/api/orders' -ContentType 'application/json' -Body (@{
        customerName = 'Cliente EKS Lab'
        vehiclePlate = 'EKS1234'
        description = 'Teste fim a fim da Saga no EKS'
    } | ConvertTo-Json)
    $orderId = $order.id
    $budgets = Wait-For 'http://localhost:28082/api/budgets' { @($args[0] | Where-Object orderId -eq $orderId).Count -gt 0 }
    $budgetId = @($budgets | Where-Object orderId -eq $orderId)[0].id

    Invoke-RestMethod -Method Patch -Uri "http://localhost:28082/api/budgets/$budgetId/amount" -ContentType 'application/json' -Body '{"amount":154}' | Out-Null
    Invoke-RestMethod -Method Post -Uri "http://localhost:28082/api/budgets/$budgetId/approve" | Out-Null
    Wait-For "http://localhost:28081/api/orders/$orderId" { $args[0].status -eq 'AGUARDANDO_PAGAMENTO' } | Out-Null
    Invoke-RestMethod -Method Post -Uri "http://localhost:28082/api/budgets/$budgetId/payment" | Out-Null
    Wait-For "http://localhost:28081/api/orders/$orderId" { $args[0].status -eq 'AGUARDANDO_EXECUCAO' } | Out-Null
    Wait-For 'http://localhost:28083/api/execution' { @($args[0] | Where-Object orderId -eq $orderId).Count -gt 0 } | Out-Null

    foreach ($status in @('EM_DIAGNOSTICO', 'EM_REPARO', 'FINALIZADA')) {
        Invoke-RestMethod -Method Patch -Uri "http://localhost:28083/api/execution/$orderId/status" -ContentType 'application/json' -Body (@{ status = $status; reason = $null } | ConvertTo-Json) | Out-Null
    }
    $final = Wait-For "http://localhost:28081/api/orders/$orderId" { $args[0].status -eq 'FINALIZADA' }
    Write-Output "Saga no EKS concluida: OS $orderId, orcamento $budgetId, status $($final.status)"
} finally {
    $forwards | ForEach-Object { if ($_ -and -not $_.HasExited) { Stop-Process -Id $_.Id -Force } }
}
