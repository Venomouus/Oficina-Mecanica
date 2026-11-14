param(
    [string]$Region = 'sa-east-1',
    [string]$Profile
)

$ErrorActionPreference = 'Stop'
$awsCommand = Get-Command aws -ErrorAction Stop
$common = @('--region', $Region, '--output', 'json', '--no-cli-pager')
if ($Profile) { $common += @('--profile', $Profile) }

function Read-AwsInventory {
    param([string[]]$Arguments)
    $result = & $awsCommand.Source @Arguments @common 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($result -join [Environment]::NewLine)
    }
    return (($result -join [Environment]::NewLine) | ConvertFrom-Json)
}

# Fail before collecting anything if the local AWS session is unavailable.
$identity = Read-AwsInventory -Arguments @('sts', 'get-caller-identity')
$report = [ordered]@{ region = $Region; identity = $identity; collectedAt = (Get-Date).ToString('o') }
$queries = [ordered]@{
    instances = @('ec2', 'describe-instances', '--query', 'Reservations[].Instances[].{Id:InstanceId,State:State.Name,Type:InstanceType,Vpc:VpcId,Subnet:SubnetId,PublicIp:PublicIpAddress}')
    volumes = @('ec2', 'describe-volumes', '--query', 'Volumes[].{Id:VolumeId,SizeGiB:Size,State:State,Type:VolumeType}')
    publicAddresses = @('ec2', 'describe-addresses', '--query', 'Addresses[].{Ip:PublicIp,Allocation:AllocationId,Instance:InstanceId}')
    vpcs = @('ec2', 'describe-vpcs', '--query', 'Vpcs[].{Id:VpcId,Default:IsDefault,Cidr:CidrBlock}')
    subnets = @('ec2', 'describe-subnets', '--query', 'Subnets[].{Id:SubnetId,Vpc:VpcId,Zone:AvailabilityZone,PublicIpOnLaunch:MapPublicIpOnLaunch}')
    securityGroups = @('ec2', 'describe-security-groups', '--query', 'SecurityGroups[].{Id:GroupId,Name:GroupName,Vpc:VpcId,Ingress:IpPermissions}')
    natGateways = @('ec2', 'describe-nat-gateways', '--query', 'NatGateways[].{Id:NatGatewayId,State:State,Vpc:VpcId}')
    loadBalancers = @('elbv2', 'describe-load-balancers', '--query', 'LoadBalancers[].{Name:LoadBalancerName,Arn:LoadBalancerArn,Type:Type,Vpc:VpcId}')
    databases = @('rds', 'describe-db-instances', '--query', 'DBInstances[].{Id:DBInstanceIdentifier,Engine:Engine,Class:DBInstanceClass,Status:DBInstanceStatus,Endpoint:Endpoint.Address}')
    clusters = @('ecs', 'list-clusters')
    repositories = @('ecr', 'describe-repositories', '--query', 'repositories[].{Name:repositoryName,Uri:repositoryUri}')
}
foreach ($name in $queries.Keys) {
    Write-Host "Consultando $name..."
    try { $report[$name] = Read-AwsInventory -Arguments $queries[$name] }
    catch { $report[$name] = @{ error = $_.Exception.Message } }
}

$outputFile = Join-Path $PSScriptRoot 'aws-inventory.local.json'
$report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $outputFile -Encoding UTF8
Write-Host "Inventario salvo em $outputFile"
Write-Host 'Nenhum recurso foi criado, alterado ou excluido. Valores de segredos nao foram consultados.'
