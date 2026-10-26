[CmdletBinding()]
param([ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')][string]$Configuration = 'EmailOutboxQa')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'database-environment.ps1')
$values = Read-LocalPostgresEnvironment -Path (Join-Path $repositoryRoot '.env')
# The account test factory only permits this isolated namespace on loopback.
$database = 'scai_audit_test_' + [guid]::NewGuid().ToString('N')
if ($database -notmatch '^scai_audit_test_[a-f0-9]{32}$') { throw 'Unsafe isolated outbox database name.' }
Add-Type -AssemblyName System.Data
$connection = New-Object System.Data.Common.DbConnectionStringBuilder
$connection['Host'] = '127.0.0.1'; $connection['Port'] = '5432'; $connection['Database'] = $database
$connection['Username'] = $values['POSTGRES_USER']; $connection['Password'] = $values['POSTGRES_PASSWORD']
$previous = [Environment]::GetEnvironmentVariable('SECURITY_CENTER_AUDIT_TEST_CONNECTION', 'Process')
$created = $false
Push-Location -LiteralPath $repositoryRoot
try {
    docker compose exec -T postgres sh -c 'createdb --username="$POSTGRES_USER" --template=template0 "$1"' outbox-tests $database
    if ($LASTEXITCODE -ne 0) { throw 'Could not create isolated outbox database.' }
    $created = $true
    [Environment]::SetEnvironmentVariable('SECURITY_CENTER_AUDIT_TEST_CONNECTION', $connection.ConnectionString, 'Process')
    dotnet test 'tests/SecurityCenterAI.Tests/SecurityCenterAI.Tests.csproj' --configuration $Configuration --filter 'FullyQualifiedName~AccountEmailOutboxPostgresTests|FullyQualifiedName~EmailQueueStatusPostgresTests' --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw 'Isolated outbox PostgreSQL checks failed.' }
    Write-Output 'Encrypted outbox, transactional writes, concurrent claims and restart checks passed.'
}
finally {
    [Environment]::SetEnvironmentVariable('SECURITY_CENTER_AUDIT_TEST_CONNECTION', $previous, 'Process')
    if ($created) {
        docker compose exec -T postgres sh -c 'dropdb --force --username="$POSTGRES_USER" "$1"' outbox-tests $database
        if ($LASTEXITCODE -ne 0) { Write-Warning ('Could not remove isolated database ' + $database) }
        else { Write-Output 'Isolated outbox database removed.' }
    }
    Pop-Location
}
