[CmdletBinding()]
param(
    [ValidateSet('help', 'menu', 'pair', 'scan', 'scan-send', 'send', 'heartbeat', 'status', 'doctor', 'version', 'forget')]
    [string]$Action = 'help',
    [string]$ApiUrl = 'http://localhost:5169',
    [switch]$Replace
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$agentPath = Join-Path $PSScriptRoot 'SecurityCenterAI.Agent.exe'
if (-not (Test-Path -LiteralPath $agentPath -PathType Leaf)) {
    throw 'No se encontro el ejecutable del agente. Extrae el ZIP completo antes de usarlo.'
}
if ($Action -ne 'pair' -and ($PSBoundParameters.ContainsKey('ApiUrl') -or $Replace)) {
    throw 'ApiUrl y Replace solo se admiten para pair.'
}

function Invoke-AgentCommand([string]$SelectedAction, [string]$Endpoint, [bool]$ReplaceCredential) {
    $agentArguments = @($SelectedAction)
    if ($SelectedAction -eq 'pair') {
        $agentArguments += @('--api', $Endpoint)
        if ($ReplaceCredential) { $agentArguments += '--replace' }
    }
    if ($SelectedAction -eq 'scan-send') { $agentArguments = @('scan', '--send') }
    # El codigo de vinculacion lo solicita el ejecutable con entrada oculta.
    & $agentPath @agentArguments
    $script:agentExitCode = $LASTEXITCODE
}

if ($Action -ne 'menu') {
    Invoke-AgentCommand $Action $ApiUrl ([bool]$Replace)
    exit $script:agentExitCode
}

while ($true) {
    Write-Host ''
    Write-Host 'Security Center AI - Agente Windows portatil'
    Write-Host '1. Comprobar requisitos (doctor)'
    Write-Host '2. Ver version'
    Write-Host '3. Consultar estado local'
    Write-Host '4. Vincular con un codigo nuevo'
    Write-Host '5. Analizar y guardar solo en este equipo'
    Write-Host '6. Analizar y enviar a la cuenta vinculada'
    Write-Host '7. Enviar el ultimo informe local'
    Write-Host '8. Confirmar contacto con la API'
    Write-Host '9. Ver ayuda'
    Write-Host '0. Salir'
    $selection = Read-Host 'Elegi una opcion'
    $selectedAction = switch ($selection) {
        '1' { 'doctor' }
        '2' { 'version' }
        '3' { 'status' }
        '4' { 'pair' }
        '5' { 'scan' }
        '6' { 'scan-send' }
        '7' { 'send' }
        '8' { 'heartbeat' }
        '9' { 'help' }
        '0' { exit 0 }
        default { '' }
    }
    if (-not $selectedAction) { Write-Host 'Opcion invalida.'; continue }
    $selectedEndpoint = 'http://localhost:5169'
    if ($selectedAction -eq 'pair') {
        $enteredEndpoint = Read-Host 'URL de la API (Enter: http://localhost:5169)'
        if (-not [string]::IsNullOrWhiteSpace($enteredEndpoint)) { $selectedEndpoint = $enteredEndpoint.Trim() }
    }
    Invoke-AgentCommand $selectedAction $selectedEndpoint $false
    if ($script:agentExitCode -ne 0) { Write-Host "La operacion termino con codigo $script:agentExitCode. Revisa el mensaje anterior." }
}
