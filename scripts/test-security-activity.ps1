[CmdletBinding()]
param([ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')][string]$Configuration = 'AuditQa')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'database-environment.ps1')
$values = Read-LocalPostgresEnvironment -Path (Join-Path $repositoryRoot '.env')
$database = 'scai_audit_test_' + [guid]::NewGuid().ToString('N')
if ($database -notmatch '^scai_audit_test_[a-f0-9]{32}$') { throw 'Unsafe isolated audit database name.' }
Add-Type -AssemblyName System.Data
$connection = New-Object System.Data.Common.DbConnectionStringBuilder
$connection['Host'] = '127.0.0.1'; $connection['Port'] = '5432'; $connection['Database'] = $database
$connection['Username'] = $values['POSTGRES_USER']; $connection['Password'] = $values['POSTGRES_PASSWORD']
$previous = [Environment]::GetEnvironmentVariable('SECURITY_CENTER_AUDIT_TEST_CONNECTION', 'Process')
$created = $false
Push-Location -LiteralPath $repositoryRoot
try {
    docker compose exec -T postgres sh -c 'createdb --username="$POSTGRES_USER" --template=template0 "$1"' audit-tests $database
    if ($LASTEXITCODE -ne 0) { throw 'Could not create isolated audit database.' }
    $created = $true
    [Environment]::SetEnvironmentVariable('SECURITY_CENTER_AUDIT_TEST_CONNECTION', $connection.ConnectionString, 'Process')
    dotnet test 'tests/SecurityCenterAI.Tests/SecurityCenterAI.Tests.csproj' --configuration $Configuration --filter 'FullyQualifiedName~SecurityActivityPostgresTests' --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw 'Isolated audit PostgreSQL checks failed.' }
    Write-Output 'Atomic audit writes, cascades, owner isolation and concurrency checks passed.'
}
finally {
    [Environment]::SetEnvironmentVariable('SECURITY_CENTER_AUDIT_TEST_CONNECTION', $previous, 'Process')
    if ($created) {
        docker compose exec -T postgres sh -c 'dropdb --force --username="$POSTGRES_USER" "$1"' audit-tests $database
        if ($LASTEXITCODE -ne 0) { Write-Warning ('Could not remove isolated database ' + $database) }
        else { Write-Output 'Isolated audit database removed.' }
    }
    Pop-Location
}
