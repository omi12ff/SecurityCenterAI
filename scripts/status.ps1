[CmdletBinding()]
param([ValidateRange(1024, 65535)][int]$Port = 5169,
    [ValidateRange(1024, 65535)][int]$SmtpPort = 1025,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')][string]$Configuration = 'StatusTools',
    [ValidateRange(1, 1000000)][int]$QueueWarningCount = 100,
    [ValidateRange(1, 10080)][int]$QueueWarningAgeMinutes = 10,
    [ValidatePattern('^scai_audit_test_[a-f0-9]{32}$')][string]$DatabaseName,
    [switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$baseUrl = "http://localhost:$Port"
$checks = @(
    @{ Label = 'API HTTP'; Path = '/health'; Expected = 'healthy' },
    @{ Label = 'PostgreSQL desde la API'; Path = '/health/ready'; Expected = 'ready' }
)
$unavailable = $false
$queueAttention = $false
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
try {
    $queueArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'email-queue-status.ps1'),
        '-Configuration', $Configuration, '-WarningCount', $QueueWarningCount, '-WarningAgeMinutes', $QueueWarningAgeMinutes)
    if ($NoBuild) { $queueArguments += '-NoBuild' }
    if ($DatabaseName) { $queueArguments += @('-DatabaseName', $DatabaseName) }
    $queueOutput = & powershell @queueArguments
    $queueExit = $LASTEXITCODE
    if ($queueExit -notin @(0, 3)) { throw 'Queue status unavailable.' }
    $queue = $queueOutput | ConvertFrom-Json
    if ($queue.reportVersion -ne 1 -or $queue.status -notin @('ok','attention')) { throw 'Invalid queue status.' }
    Write-Host 'Cola de correo persistente: consulta disponible.'
    Write-Host ("  Total: {0}; listos: {1}; en espera: {2}; reservados: {3}; vencidos: {4}; sin intentos: {5}." -f
        $queue.counts.total, $queue.counts.ready, $queue.counts.waiting, $queue.counts.reserved, $queue.counts.expired, $queue.counts.exhausted)
    Write-Host ("  Pendientes con intentos previos: {0}. Limite de intentos leido: {1}." -f $queue.counts.retryPending, $queue.deliveryAttemptLimit)
    if ($null -eq $queue.oldestAgeSeconds) { Write-Host '  Antiguedad: sin pendientes.' }
    else { Write-Host ("  Antiguedad mayor: {0:N1} minutos; creado {1:u} (UTC)." -f
        ($queue.oldestAgeSeconds / 60.0), ([datetime]$queue.oldestCreatedAtUtc).ToUniversalTime()) }
    if ($null -eq $queue.nextAttemptAtUtc) { Write-Host '  Proximo intento sin reserva: ninguno pendiente.' }
    elseif ([datetime]$queue.nextAttemptAtUtc -le [datetime]$queue.checkedAtUtc) { Write-Host '  Proximo intento sin reserva: habilitado ahora.' }
    else { Write-Host ("  Proximo intento sin reserva: {0:u} (UTC)." -f ([datetime]$queue.nextAttemptAtUtc).ToUniversalTime()) }
    if ($null -ne $queue.nextReservationExpiryAtUtc) {
        Write-Host ("  Proxima reserva que vence: {0:u} (UTC)." -f ([datetime]$queue.nextReservationExpiryAtUtc).ToUniversalTime())
    }
    if ($queue.status -eq 'attention') {
        $queueAttention = $true
        $labels = @{
            'queue_count_threshold' = 'cantidad de pendientes en el umbral'
            'oldest_age_threshold' = 'antiguedad en el umbral'
            'expired_pending' = 'vencidos pendientes de retirada'
            'attempts_exhausted' = 'intentos agotados pendientes de retirada'
        }
        foreach ($warning in $queue.warnings) { if ($labels.ContainsKey($warning)) { Write-Host ("  Para revisar: {0}." -f $labels[$warning]) } }
    }
}
catch { $unavailable = $true; Write-Host 'Cola de correo persistente: no disponible o no comprobada; revisa herramientas, conexion, permisos y migracion.' }
Write-Host 'Consulta local de solo lectura, sin envio ni descifrado. No prueba entrega final, worker activo, TLS SMTP de produccion, esquema completo, agente ni backups.'
if ($unavailable) { exit 1 }
if ($queueAttention) { exit 3 }
exit 0
