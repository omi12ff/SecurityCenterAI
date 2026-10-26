[CmdletBinding()]
param(
    [ValidateSet('create', 'verify', 'rehearse')][string]$Action = 'create',
    [string]$File,
    [string]$Directory = (Join-Path $env:LOCALAPPDATA 'SecurityCenterAI\Backups'),
    [System.Security.SecureString]$Passphrase,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')][string]$Configuration = 'BackupTools',
    [ValidatePattern('^scai_backup_fixture_[a-f0-9]{32}$')][string]$DatabaseName,
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Action -in @('verify','rehearse') -and [string]::IsNullOrWhiteSpace($File)) { throw 'Verify/rehearse requires -File.' }
if ($Action -eq 'create' -and $File) { throw 'Create chooses a new filename; omit -File.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'database-environment.ps1')
$values = Read-LocalPostgresEnvironment -Path (Join-Path $repositoryRoot '.env')
Add-Type -AssemblyName System.Data
$connection = New-Object System.Data.Common.DbConnectionStringBuilder
$connection['Host'] = '127.0.0.1'; $connection['Port'] = '5432'
$connection['Database'] = $(if ($DatabaseName) { $DatabaseName } else { $values['POSTGRES_DB'] })
$connection['Username'] = $values['POSTGRES_USER']; $connection['Password'] = $values['POSTGRES_PASSWORD']
$assemblyPath = Join-Path $repositoryRoot "src\SecurityCenterAI.Api\bin\$Configuration\net8.0\SecurityCenterAI.Api.dll"
$executablePath = [IO.Path]::ChangeExtension($assemblyPath, '.exe')
$dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
if (-not $NoBuild) {
    $active = @(Get-CimInstance Win32_Process -Filter "Name = 'SecurityCenterAI.Api.exe' OR Name = 'dotnet.exe'" |
        Where-Object { $_.ExecutablePath -ieq $executablePath -or ($_.CommandLine -and $_.CommandLine.Contains($assemblyPath)) })
    if ($active.Count -gt 0) { throw 'Build configuration is in use. Choose another -Configuration.' }
    & $dotnet build (Join-Path $repositoryRoot 'src\SecurityCenterAI.Api\SecurityCenterAI.Api.csproj') --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Backup tool build failed.' }
}
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) { throw 'Backup binary missing. Omit -NoBuild first.' }

function Read-SecretText([System.Security.SecureString]$Secret) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secret)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}
function Quote-ProcessArgument([string]$Value) {
    return '"' + ([regex]::Replace($Value, '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
}

$prompted = $false; $confirmation = $null; $passwordText = $null; $confirmText = $null; $process = $null
try {
    if ($null -eq $Passphrase) { $Passphrase = Read-Host 'Contrasena de la copia (minimo 16 caracteres; guardala fuera del proyecto)' -AsSecureString; $prompted = $true }
    $passwordText = Read-SecretText $Passphrase
    if ([string]::IsNullOrWhiteSpace($passwordText) -or $passwordText.Length -lt 16 -or $passwordText.Length -gt 1024 -or $passwordText.Contains("`r") -or $passwordText.Contains("`n")) { throw 'Use a passphrase of 16 to 1024 characters on one line.' }
    if ($prompted -and $Action -eq 'create') {
        $confirmation = Read-Host 'Repeti la contrasena de la copia' -AsSecureString
        $confirmText = Read-SecretText $confirmation
        if (-not [string]::Equals($passwordText, $confirmText, [StringComparison]::Ordinal)) { throw 'Passphrases do not match.' }
    }
    $mode = '--backup-create'; $targetFlag = '--directory'; $target = [IO.Path]::GetFullPath($Directory)
    if ($Action -in @('verify','rehearse')) {
        $mode = $(if ($Action -eq 'rehearse') { '--backup-rehearse' } else { '--backup-verify' })
        $targetFlag = '--file'; $target = [IO.Path]::GetFullPath($File)
    }
    $arguments = @($assemblyPath, $mode, '--repository', $repositoryRoot, $targetFlag, $target)
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $dotnet
    $start.Arguments = (($arguments | ForEach-Object { Quote-ProcessArgument $_ }) -join ' ')
    $start.WorkingDirectory = $repositoryRoot
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.EnvironmentVariables['ConnectionStrings__PostgreSQL'] = $connection.ConnectionString
    $process = [Diagnostics.Process]::Start($start)
    $outputTask = $process.StandardOutput.ReadToEndAsync(); $errorTask = $process.StandardError.ReadToEndAsync()
    $passwordBytes = [Text.Encoding]::UTF8.GetBytes($passwordText + "`n")
    try { $process.StandardInput.BaseStream.Write($passwordBytes, 0, $passwordBytes.Length); $process.StandardInput.BaseStream.Flush() }
    finally { [Array]::Clear($passwordBytes, 0, $passwordBytes.Length); $process.StandardInput.Close() }
    $passwordText = $null; $confirmText = $null
    $process.WaitForExit()
    $outputText = $outputTask.GetAwaiter().GetResult(); $errorText = $errorTask.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) { throw ("Backup operation failed (exit " + $process.ExitCode + '). ' + $errorText.Trim()) }
    Write-Output $outputText.TrimEnd()
}
finally {
    $passwordText = $null; $confirmText = $null
    if ($confirmation) { $confirmation.Dispose() }
    if ($prompted -and $Passphrase) { $Passphrase.Dispose() }
    if ($process) { if (-not $process.HasExited) { $process.Kill() }; $process.Dispose() }
}
