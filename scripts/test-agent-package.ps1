[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$testScript = Join-Path (Split-Path -Parent $PSScriptRoot) 'tests\packaging\Invoke-AgentPackageTests.ps1'
& $testScript -ArchivePath $ArchivePath
