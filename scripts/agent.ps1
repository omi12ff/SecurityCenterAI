[CmdletBinding()]
param(
    [ValidateSet('pair', 'heartbeat', 'status', 'scan', 'send', 'forget', 'help')]
    [string]$Action = 'help',
    [string]$ApiUrl = 'http://localhost:5169',
    [switch]$Replace,
    [switch]$Send
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Action -ne 'pair' -and ($PSBoundParameters.ContainsKey('ApiUrl') -or $Replace)) {
    throw 'ApiUrl y Replace solo se admiten para pair.'
}
if ($Send -and $Action -ne 'scan') { throw 'Send solo se admite para scan.' }

$projectPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\SecurityCenterAI.Agent\SecurityCenterAI.Agent.csproj'
$agentArguments = @('run', '--project', $projectPath, '--', $Action)
if ($Action -eq 'pair') {
    $agentArguments += @('--api', $ApiUrl)
    if ($Replace) { $agentArguments += '--replace' }
}
if ($Action -eq 'scan' -and $Send) { $agentArguments += '--send' }

# The code is read by the agent, never by this wrapper or a command-line argument.
& dotnet @agentArguments
exit $LASTEXITCODE
