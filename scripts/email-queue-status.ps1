[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')][string]$Configuration = 'StatusTools',
    [ValidateRange(1, 1000000)][int]$WarningCount = 100,
    [ValidateRange(1, 10080)][int]$WarningAgeMinutes = 10,
    [ValidatePattern('^scai_audit_test_[a-f0-9]{32}$')][string]$DatabaseName,
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$apiRoot = Join-Path $repositoryRoot 'src\SecurityCenterAI.Api'
$assembly = Join-Path $apiRoot ("bin\$Configuration\net8.0\SecurityCenterAI.Api.dll")
$executable = [IO.Path]::ChangeExtension($assembly, '.exe')
$process = $null
try {
    . (Join-Path $PSScriptRoot 'database-environment.ps1')
    $values = Read-LocalPostgresEnvironment -Path (Join-Path $repositoryRoot '.env')
    $dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    if (-not $NoBuild) {
        $active = @(Get-CimInstance Win32_Process -Filter "Name = 'SecurityCenterAI.Api.exe' OR Name = 'dotnet.exe'" |
            Where-Object { $_.ExecutablePath -ieq $executable -or ($_.CommandLine -and $_.CommandLine.Contains($assembly)) })
        if ($active.Count -gt 0) { throw 'Configuration in use.' }
        $buildOutput = & $dotnet build (Join-Path $apiRoot 'SecurityCenterAI.Api.csproj') --configuration $Configuration --nologo --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) { throw 'Diagnostic build failed.' }
    }
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw 'Diagnostic binary missing.' }
    Add-Type -AssemblyName System.Data
    $connection = New-Object System.Data.Common.DbConnectionStringBuilder
    $connection['Host'] = '127.0.0.1'; $connection['Port'] = '5432'
    $connection['Database'] = $(if ($DatabaseName) { $DatabaseName } else { $values['POSTGRES_DB'] })
    $connection['Username'] = $values['POSTGRES_USER']; $connection['Password'] = $values['POSTGRES_PASSWORD']
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $dotnet
    $start.Arguments = '"' + $assembly + '" --email-queue-status --warning-count ' + $WarningCount + ' --warning-age-minutes ' + $WarningAgeMinutes
    $start.WorkingDirectory = $apiRoot
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    # Keep the connection out of command arguments and restore nothing in the parent.
    $start.EnvironmentVariables['ConnectionStrings__PostgreSQL'] = $connection.ConnectionString
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $start
    [void]$process.Start()
    $outputTask = $process.StandardOutput.ReadToEndAsync()
    $errorTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(20000)) { $process.Kill(); $process.WaitForExit(); throw 'Diagnostic timed out.' }
    $output = $outputTask.GetAwaiter().GetResult()
    $discardedError = $errorTask.GetAwaiter().GetResult()
    if ($process.ExitCode -notin @(0, 3)) { throw 'Diagnostic unavailable.' }
    # Validate the response before printing; never echo untrusted errors/build output.
    $report = $output | ConvertFrom-Json
    if ($report.reportVersion -ne 1 -or $report.status -notin @('ok','attention') -or
        (($process.ExitCode -eq 3) -ne ($report.status -eq 'attention'))) { throw 'Invalid diagnostic response.' }
    Write-Output $output
    exit $process.ExitCode
}
catch { Write-Output 'No se pudo consultar la cola. Revisa compilacion, configuracion, permisos, conexion y migracion; no se muestran datos privados.'; exit 1 }
finally { if ($null -ne $process) { $process.Dispose() } }
