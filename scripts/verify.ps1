[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')][string]$Configuration = 'VerificationQa',
    [switch]$IncludePostgres,
    [switch]$IncludeE2E
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$webRoot = Join-Path $repositoryRoot 'src\SecurityCenterAI.Web'
$nextEnvPath = Join-Path $webRoot 'next-env.d.ts'
$originalNextEnv = [IO.File]::ReadAllBytes($nextEnvPath)
$previousQa = [Environment]::GetEnvironmentVariable('SECURITY_CENTER_WEB_QA', 'Process')
# The E2E runner generates its own loopback URLs and overrides inherited targets.
$outputRoot = Join-Path $repositoryRoot "src\SecurityCenterAI.Api\bin\$Configuration\net8.0"
$apiExecutable = [IO.Path]::GetFullPath((Join-Path $outputRoot 'SecurityCenterAI.Api.exe'))
$apiAssembly = [IO.Path]::GetFullPath((Join-Path $outputRoot 'SecurityCenterAI.Api.dll'))
foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name = 'SecurityCenterAI.Api.exe' OR Name = 'dotnet.exe'")) {
    if ($process.ExecutablePath -ieq $apiExecutable -or ($process.CommandLine -and
        $process.CommandLine -match ('(?i)(?:^|[\s"])' + [regex]::Escape($apiAssembly) + '(?:[\s"]|$)'))) {
        throw 'La configuracion elegida esta en uso por la API. Usa otra -Configuration antes de verificar.'
    }
}

function Invoke-Verification {
    param([string]$Command, [string[]]$Arguments, [string]$Label)
    Write-Host $Label
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Fallo la verificacion: $Label (codigo $LASTEXITCODE)." }
}

Push-Location -LiteralPath $repositoryRoot
try {
    Invoke-Verification git @('-c', 'core.safecrlf=false', 'diff', '--check') 'Comprobando cambios de Git...'
    $testProjects = @('tests\SecurityCenterAI.Tests\SecurityCenterAI.Tests.csproj', 'tests\SecurityCenterAI.Agent.Tests\SecurityCenterAI.Agent.Tests.csproj')
    foreach ($testProject in $testProjects) {
        Invoke-Verification dotnet @('build', $testProject, '--configuration', $Configuration, '--nologo', '--verbosity', 'minimal') "Compilando pruebas y dependencias en $Configuration..."
        Invoke-Verification dotnet @('test', $testProject, '--configuration', $Configuration, '--no-build', '--nologo', '--verbosity', 'minimal') "Pruebas .NET: $testProject"
    }
    if ($IncludePostgres) {
        foreach ($script in @('test-security-activity.ps1', 'test-retention.ps1', 'test-backup.ps1', 'test-email-outbox.ps1')) {
            Invoke-Verification powershell @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot $script), '-Configuration', $Configuration) "Pruebas PostgreSQL aisladas: $script"
        }
    }
    else { Write-Host 'PostgreSQL aislado omitido; usa -IncludePostgres para incluirlo.' }
    Push-Location -LiteralPath $webRoot
    try {
        Invoke-Verification npm.cmd @('test') 'Pruebas unitarias web...'
        [Environment]::SetEnvironmentVariable('SECURITY_CENTER_WEB_QA', '1', 'Process')
        Invoke-Verification npm.cmd @('run', 'typecheck') 'Tipos web con salida QA...'
        Invoke-Verification npm.cmd @('run', 'build') 'Compilacion web de produccion con salida QA...'
        if ($IncludeE2E) {
            [Environment]::SetEnvironmentVariable('SECURITY_CENTER_WEB_QA', $previousQa, 'Process')
            Invoke-Verification node @((Join-Path $repositoryRoot 'scripts\test-web-e2e.mjs'), '--configuration', $Configuration, '--no-build') 'E2E con base, API y web aisladas...'
        }
        else { Write-Host 'E2E omitido; usa -IncludeE2E con PostgreSQL y Mailpit locales disponibles; API y web se inician aparte.' }
    }
    finally { Pop-Location }
    Write-Host 'Verificacion solicitada completada. Consulta las omisiones de cada suite; no implica preparacion para produccion.'
}
finally {
    [Environment]::SetEnvironmentVariable('SECURITY_CENTER_WEB_QA', $previousQa, 'Process')
    [IO.File]::WriteAllBytes($nextEnvPath, $originalNextEnv)
    Pop-Location
}
