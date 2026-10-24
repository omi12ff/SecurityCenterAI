[CmdletBinding()]
param([ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')][string]$Configuration = 'BackupQa')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'database-environment.ps1')
$values = Read-LocalPostgresEnvironment -Path (Join-Path $repositoryRoot '.env')
$fixtureDatabase = 'scai_backup_fixture_' + [guid]::NewGuid().ToString('N')
if ($fixtureDatabase -notmatch '^scai_backup_fixture_[a-f0-9]{32}$') { throw 'Unsafe fixture database name.' }
$testDirectory = Join-Path $env:LOCALAPPDATA ('SecurityCenterAI\Backups\test_' + [guid]::NewGuid().ToString('N'))
$backupStore = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'SecurityCenterAI\Backups'))
Add-Type -AssemblyName System.Data
$connection = New-Object System.Data.Common.DbConnectionStringBuilder
$connection['Host'] = '127.0.0.1'; $connection['Port'] = '5432'; $connection['Database'] = $fixtureDatabase
$connection['Username'] = $values['POSTGRES_USER']; $connection['Password'] = $values['POSTGRES_PASSWORD']
$previousConnection = [Environment]::GetEnvironmentVariable('SECURITY_CENTER_BACKUP_TEST_CONNECTION', 'Process')
$created = $false; $passphrase = $null; $incorrect = $null
Push-Location -LiteralPath $repositoryRoot
try {
    docker compose exec -T postgres sh -c 'createdb --username="$POSTGRES_USER" --template=template0 "$1"' backup-tests $fixtureDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Could not create isolated backup fixture.' }
    $created = $true
    [Environment]::SetEnvironmentVariable('SECURITY_CENTER_BACKUP_TEST_CONNECTION', $connection.ConnectionString, 'Process')
    dotnet test 'tests/SecurityCenterAI.Tests/SecurityCenterAI.Tests.csproj' --configuration $Configuration --filter 'FullyQualifiedName~BackupPostgresTests' --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw 'Isolated PostgreSQL backup tests failed.' }
    $passphrase = ConvertTo-SecureString ([guid]::NewGuid().ToString('N') + ' ficticia ' + [char]0x00F1) -AsPlainText -Force
    $helper = Join-Path $PSScriptRoot 'backup.ps1'
    $lines = & $helper -Action create -Directory $testDirectory -DatabaseName $fixtureDatabase -Passphrase $passphrase -Configuration $Configuration -NoBuild
    $result = ($lines -join [Environment]::NewLine) | ConvertFrom-Json
    if ($result.status -ne 'created-and-verified' -or $result.tablesVerified -lt 13) { throw 'Backup create CLI contract failed.' }
    $lines = & $helper -Action verify -File $result.file -DatabaseName $fixtureDatabase -Passphrase $passphrase -Configuration $Configuration -NoBuild
    $verified = ($lines -join [Environment]::NewLine) | ConvertFrom-Json
    if ($verified.status -ne 'verified') { throw 'Backup verification CLI contract failed.' }
    $incorrect = ConvertTo-SecureString 'incorrect fictitious backup passphrase' -AsPlainText -Force
    $rejected = $false
    try { & $helper -Action verify -File $result.file -DatabaseName $fixtureDatabase -Passphrase $incorrect -Configuration $Configuration -NoBuild }
    catch { if ($_.Exception.Message -like '*exit 5*') { $rejected = $true } else { throw } }
    if (-not $rejected) { throw 'Wrong-password CLI check failed.' }
    Write-Output 'Isolated backup/restore, CLI and wrong-password checks passed.'
}
finally {
    [Environment]::SetEnvironmentVariable('SECURITY_CENTER_BACKUP_TEST_CONNECTION', $previousConnection, 'Process')
    if ($created) {
        docker compose exec -T postgres sh -c 'dropdb --force --username="$POSTGRES_USER" "$1"' backup-tests $fixtureDatabase
        if ($LASTEXITCODE -ne 0) { Write-Warning ('Could not remove isolated fixture ' + $fixtureDatabase) }
        else { Write-Output 'Isolated backup fixture database removed.' }
    }
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($testDirectory)) -ne $backupStore -or [IO.Path]::GetFileName($testDirectory) -notmatch '^test_[a-f0-9]{32}$') { throw 'Unsafe test folder cleanup target.' }
    if (Test-Path -LiteralPath $testDirectory -PathType Container) {
        Get-ChildItem -LiteralPath $testDirectory -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
        [IO.Directory]::Delete($testDirectory, $false)
    }
    if ($passphrase) { $passphrase.Dispose() }; if ($incorrect) { $incorrect.Dispose() }
    Pop-Location
}
