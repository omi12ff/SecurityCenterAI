[CmdletBinding()]
param([ValidateRange(1024, 65535)][int]$Port = 5169,
    [ValidateRange(1024, 65535)][int]$SmtpPort = 1025)

$ErrorActionPreference = 'Stop'
$baseUrl = "http://localhost:$Port"
$checks = @(
    @{ Label = 'API HTTP'; Path = '/health'; Expected = 'healthy' },
    @{ Label = 'PostgreSQL desde la API'; Path = '/health/ready'; Expected = 'ready' }
)
$unavailable = $false
foreach ($check in $checks) {
    try {
        $response = Invoke-RestMethod -Uri ($baseUrl + $check.Path) -TimeoutSec 6
        if ($response.status -ne $check.Expected) { throw 'Unexpected health status.' }
        Write-Host "$($check.Label): disponible."
    }
    catch {
        $unavailable = $true
        Write-Host "$($check.Label): no disponible o no comprobado."
    }
}
$smtpClient = [Net.Sockets.TcpClient]::new()
$reader = $null
$writer = $null
try {
    $connected = $smtpClient.ConnectAsync('localhost', $SmtpPort)
    if (-not $connected.Wait(3000)) { throw 'SMTP connect timeout.' }
    $stream = $smtpClient.GetStream()
    $stream.ReadTimeout = 3000
    $stream.WriteTimeout = 3000
    $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::ASCII, $false, 1024, $true)
    $writer = [IO.StreamWriter]::new($stream, [Text.Encoding]::ASCII, 1024, $true)
    $writer.NewLine = "`r`n"
    $writer.AutoFlush = $true
    if ($reader.ReadLine() -notmatch '^220(?: |$)') { throw 'Unexpected SMTP greeting.' }
    $writer.WriteLine('NOOP')
    if ($reader.ReadLine() -notmatch '^250(?: |$)') { throw 'SMTP NOOP failed.' }
    $writer.WriteLine('QUIT')
    Write-Host 'Capturador SMTP local: responde a NOOP; no se envio correo.'
}
catch {
    $unavailable = $true
    Write-Host 'Capturador SMTP local: no disponible o no comprobado.'
}
finally {
    if ($null -ne $writer) { $writer.Dispose() }
    if ($null -ne $reader) { $reader.Dispose() }
    $smtpClient.Dispose()
}
Write-Host 'Consulta local sin envio de correo. No verifica entrega, TLS/credenciales SMTP de produccion, esquema, agente ni backups.'
if ($unavailable) { exit 1 }
exit 0
