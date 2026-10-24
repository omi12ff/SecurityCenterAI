[CmdletBinding()]
param(
    [ValidateSet('preview', 'apply')][string]$Action = 'preview',
    [switch]$ConfirmPurge,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')][string]$Configuration = 'RetentionTools',
    [ValidatePattern('^scai_retention_test_[a-f0-9]{32}$')][string]$DatabaseName,
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Action -eq 'apply' -and -not $ConfirmPurge) { throw 'Run preview first. Apply requires -ConfirmPurge.' }
if ($Action -eq 'preview' -and $ConfirmPurge) { throw 'ConfirmPurge is only valid with -Action apply.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'database-environment.ps1')
$values = Read-LocalPostgresEnvironment -Path (Join-Path $repositoryRoot '.env')
Add-Type -AssemblyName System.Data
$connection = New-Object System.Data.Common.DbConnectionStringBuilder
$connection['Host'] = '127.0.0.1'
$connection['Port'] = '5432'
$connection['Database'] = $(if ($DatabaseName) { $DatabaseName } else { $values['POSTGRES_DB'] })
$connection['Username'] = $values['POSTGRES_USER']
$connection['Password'] = $values['POSTGRES_PASSWORD']
$apiRoot = Join-Path $repositoryRoot 'src\SecurityCenterAI.Api'
$assembly = Join-Path $apiRoot ("bin\$Configuration\net8.0\SecurityCenterAI.Api.dll")
$executable = [IO.Path]::ChangeExtension($assembly, '.exe')
$dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$previousEnvironment = @{}
Push-Location -LiteralPath $apiRoot
try {
    if (-not $NoBuild) {
        $active = @(Get-CimInstance Win32_Process -Filter "Name = 'SecurityCenterAI.Api.exe' OR Name = 'dotnet.exe'" |
            Where-Object { $_.ExecutablePath -ieq $executable -or ($_.CommandLine -and $_.CommandLine.Contains($assembly)) })
        if ($active.Count -gt 0) { throw 'Build configuration is in use. Choose another -Configuration.' }
        & $dotnet build (Join-Path $apiRoot 'SecurityCenterAI.Api.csproj') --configuration $Configuration --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Maintenance build failed.' }
    }
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw 'Maintenance binary not found. Omit -NoBuild first.' }
    $settings = @{
        'ConnectionStrings__PostgreSQL' = $connection.ConnectionString
        'Retention__Enabled' = $(if ($Action -eq 'apply') { 'true' } else { 'false' })
    }
    foreach ($name in $settings.Keys) {
        $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $settings[$name], 'Process')
    }
    $arguments = @($assembly, '--retention-preview')
    if ($Action -eq 'apply') { $arguments = @($assembly, '--retention-apply', '--confirm-retention') }
    & $dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Maintenance did not complete (exit $LASTEXITCODE)." }
}
finally {
    foreach ($name in $previousEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process') }
    Pop-Location
}
