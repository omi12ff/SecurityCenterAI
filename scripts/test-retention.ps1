[CmdletBinding()]
param([ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')][string]$Configuration = 'RetentionQa')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'database-environment.ps1')
$values = Read-LocalPostgresEnvironment -Path (Join-Path $repositoryRoot '.env')
$testDatabase = 'scai_retention_test_' + [guid]::NewGuid().ToString('N')
if ($testDatabase -notmatch '^scai_retention_test_[a-f0-9]{32}$') { throw 'Unsafe test database name.' }
Add-Type -AssemblyName System.Data
$connection = New-Object System.Data.Common.DbConnectionStringBuilder
$connection['Host'] = '127.0.0.1'; $connection['Port'] = '5432'
$connection['Database'] = $testDatabase; $connection['Username'] = $values['POSTGRES_USER']; $connection['Password'] = $values['POSTGRES_PASSWORD']
$previousConnection = [Environment]::GetEnvironmentVariable('SECURITY_CENTER_RETENTION_TEST_CONNECTION', 'Process')
$created = $false
Push-Location -LiteralPath $repositoryRoot
try {
    docker compose exec -T postgres sh -c 'createdb --username="$POSTGRES_USER" --template=template0 "$1"' retention-tests $testDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Could not create isolated retention database.' }
    $created = $true
    [Environment]::SetEnvironmentVariable('SECURITY_CENTER_RETENTION_TEST_CONNECTION', $connection.ConnectionString, 'Process')
    dotnet test 'tests/SecurityCenterAI.Tests/SecurityCenterAI.Tests.csproj' --configuration $Configuration --filter 'FullyQualifiedName~RetentionPostgresTests' --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw 'Isolated PostgreSQL retention checks failed.' }
    $helper = Join-Path $PSScriptRoot 'retention.ps1'
    $previewLines = & $helper -Action preview -DatabaseName $testDatabase -Configuration $Configuration -NoBuild
    $preview = ($previewLines -join [Environment]::NewLine) | ConvertFrom-Json
    if ($preview.status -ne 'preview' -or $preview.removed.sessions -ne 0) { throw 'CLI preview contract failed.' }
    $applyLines = & $helper -Action apply -ConfirmPurge -DatabaseName $testDatabase -Configuration $Configuration -NoBuild
    $applied = ($applyLines -join [Environment]::NewLine) | ConvertFrom-Json
    if ($applied.status -ne 'applied') { throw 'CLI apply contract failed.' }
    $guarded = $false
    try { & $helper -Action apply -DatabaseName $testDatabase -Configuration $Configuration -NoBuild }
    catch { if ($_.Exception.Message -like '*requires -ConfirmPurge*') { $guarded = $true } else { throw } }
    if (-not $guarded) { throw 'Apply confirmation guard failed.' }
    Write-Output 'Isolated PostgreSQL tests, CLI preview/apply and confirmation guard passed.'
}
finally {
    [Environment]::SetEnvironmentVariable('SECURITY_CENTER_RETENTION_TEST_CONNECTION', $previousConnection, 'Process')
    if ($created) {
        docker compose exec -T postgres sh -c 'dropdb --force --username="$POSTGRES_USER" "$1"' retention-tests $testDatabase
        if ($LASTEXITCODE -ne 0) { Write-Warning 'Isolated retention database cleanup failed.' }
        else { Write-Output 'Isolated retention database removed.' }
    }
    Pop-Location
}
