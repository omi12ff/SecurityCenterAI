[CmdletBinding()]
param(
    [ValidateSet('start', 'migrate', 'test')]
    [string]$Action = 'start',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')]
    [string]$Configuration = 'Debug',
    [ValidateRange(1024, 65535)]
    [int]$Port = 5169,
    [switch]$Restart
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Restart -and $Action -ne 'start') {
    throw 'Restart solo se admite para iniciar la API.'
}

function Test-ProjectApiProcess {
    param($ApiProcess, [string]$ExecutablePath, [string]$AssemblyPath)

    if ($null -eq $ApiProcess) { return $false }
    if ([string]::Equals($ApiProcess.ExecutablePath, $ExecutablePath, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }
    # Also recognize an explicit dotnet <absolute API.dll> launch. Do not infer
    # ownership from a process name, port, or health response alone.
    if ($ApiProcess.ExecutablePath -and
        [IO.Path]::GetFileName($ApiProcess.ExecutablePath) -ieq 'dotnet.exe' -and $ApiProcess.CommandLine) {
        $assemblyPattern = '(?i)(?:^|[\s"])' + [regex]::Escape($AssemblyPath) + '(?:[\s"]|$)'
        return $ApiProcess.CommandLine -match $assemblyPattern
    }
    return $false
}

function Get-DevelopmentApiState {
    param([string]$RootPath, [string]$BuildConfiguration, [string]$RequestedAction, [int]$RequestedPort)

    $outputDirectory = Join-Path $RootPath "src\SecurityCenterAI.Api\bin\$BuildConfiguration\net8.0"
    $executablePath = [IO.Path]::GetFullPath((Join-Path $outputDirectory 'SecurityCenterAI.Api.exe'))
    $assemblyPath = [IO.Path]::GetFullPath((Join-Path $outputDirectory 'SecurityCenterAI.Api.dll'))
    $running = @(Get-CimInstance Win32_Process -Filter "Name = 'SecurityCenterAI.Api.exe' OR Name = 'dotnet.exe'" |
        Where-Object { Test-ProjectApiProcess $_ $executablePath $assemblyPath })
    $state = [pscustomobject]@{ Process = $null; ExecutablePath = $executablePath; AssemblyPath = $assemblyPath }
    if ($RequestedAction -ne 'start') {
        if ($running.Count -gt 0) {
            throw "La API de este proyecto sigue usando $BuildConfiguration. Detenela con Ctrl+C en su terminal o usa otra configuracion, por ejemplo -Configuration Release, antes de $RequestedAction."
        }
        return $state
    }

    $owners = @(Get-NetTCPConnection -LocalPort $RequestedPort -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique)
    if ($owners.Count -gt 0) {
        if ($owners.Count -ne 1) { throw "El puerto $RequestedPort tiene varios procesos escuchando. Revisa sus instancias antes de iniciar la API." }
        $ownerProcess = Get-CimInstance Win32_Process -Filter ("ProcessId = " + $owners[0])
        if (-not (Test-ProjectApiProcess $ownerProcess $executablePath $assemblyPath)) {
            throw "El puerto $RequestedPort esta ocupado por otra aplicacion o configuracion. Usa otro -Port y, si hay una API activa, otra -Configuration."
        }
        if ($running.Count -ne 1) { throw "Hay varias instancias usando $BuildConfiguration. Detenelas antes de compilar." }
        $state.Process = $ownerProcess
    }
    elseif ($running.Count -gt 0) {
        throw "La API de este proyecto ya usa $BuildConfiguration en otra instancia. Cambiar solo -Port no libera sus DLL. Detenela o usa tambien otra -Configuration."
    }
    return $state
}

function Stop-DevelopmentApi {
    param($State, [int]$RequestedPort)

    $expectedProcess = $State.Process
    $currentProcess = Get-CimInstance Win32_Process -Filter ("ProcessId = " + $expectedProcess.ProcessId)
    if (-not (Test-ProjectApiProcess $currentProcess $State.ExecutablePath $State.AssemblyPath) -or
        $currentProcess.CreationDate -ne $expectedProcess.CreationDate) {
        throw 'La instancia cambio mientras se preparaba el reinicio. Vuelve a ejecutar el comando.'
    }
    Write-Host "Deteniendo la API de este proyecto en el puerto $RequestedPort..."
    Stop-Process -Id $currentProcess.ProcessId -ErrorAction Stop
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $remaining = Get-Process -Id $currentProcess.ProcessId -ErrorAction SilentlyContinue
        $listeners = @(Get-NetTCPConnection -LocalPort $RequestedPort -State Listen -ErrorAction SilentlyContinue)
        if ($null -eq $remaining -and $listeners.Count -eq 0) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'No se libero la instancia anterior. No se intentara compilar con sus archivos bloqueados.'
}

function Invoke-NativeCommand {
    param(
        [string]$FilePath,
        [string[]]$CommandArguments,
        [string]$Step
    )

    Write-Host $Step
    & $FilePath @CommandArguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Step Error de comando (codigo $LASTEXITCODE)."
    }
}

function Read-DevelopmentEnvironment {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw 'Falta .env. Copia .env.example como .env y configura sus secretos.'
    }

    $requiredNames = @('POSTGRES_DB', 'POSTGRES_USER', 'POSTGRES_PASSWORD', 'JWT_KEY')
    $values = @{}
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
        $lineNumber++
        $entry = $line.Trim()
        if ($entry.Length -eq 0 -or $entry.StartsWith('#')) {
            continue
        }

        $separator = $entry.IndexOf('=')
        if ($separator -lt 1) {
            throw "Formato .env invalido en linea $lineNumber. Usa NOMBRE=valor."
        }

        $name = $entry.Substring(0, $separator).Trim()
        if ($requiredNames -notcontains $name) {
            continue
        }
        if ($values.ContainsKey($name)) {
            throw "Variable repetida en .env: $name."
        }

        $value = $entry.Substring($separator + 1).Trim()
        if ($value.StartsWith('"') -or $value.StartsWith("'")) {
            $quote = $value.Substring(0, 1)
            if ($value.Length -lt 2 -or -not $value.EndsWith($quote)) {
                throw "Comillas sin cerrar en .env, linea $lineNumber."
            }
            $value = $value.Substring(1, $value.Length - 2)
        }
        $values[$name] = $value
    }

    foreach ($name in $requiredNames) {
        if (-not $values.ContainsKey($name) -or [string]::IsNullOrWhiteSpace($values[$name])) {
            throw "Falta un valor para $name en .env."
        }
    }
    if ($values['JWT_KEY'].Length -lt 32) {
        throw 'JWT_KEY debe tener al menos 32 caracteres.'
    }

    return $values
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$previousEnvironment = @{}
Push-Location -LiteralPath $repositoryRoot
try {
    $dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $apiState = Get-DevelopmentApiState -RootPath $repositoryRoot -BuildConfiguration $Configuration -RequestedAction $Action -RequestedPort $Port
    if ($Action -eq 'start' -and $null -ne $apiState.Process -and -not $Restart) {
        try {
            $apiHealth = Invoke-RestMethod -Uri "http://localhost:$Port/health" -TimeoutSec 3
            if ($apiHealth.status -ne 'healthy') { throw 'Estado no disponible.' }
        }
        catch { throw 'La API tiene una instancia activa pero no responde correctamente. Repite el comando con -Restart.' }
        Write-Host "La API de este proyecto ya esta iniciada en http://localhost:$Port."
        Write-Host 'No se recompilo ni se aplicaron migraciones. Para cargar cambios, repite este comando con -Restart.'
        Write-Host 'Para analizar y enviar resultados usa scripts\agent.ps1 -Action scan -Send en otra terminal.'
        return
    }

    if ($Action -eq 'test') {
        Invoke-NativeCommand -FilePath $dotnet -CommandArguments @('test', 'SecurityCenterAI.sln', '--configuration', $Configuration) -Step 'Ejecutando pruebas...'
    }
    else {
        $values = Read-DevelopmentEnvironment -Path (Join-Path $repositoryRoot '.env')
        if ($null -ne $apiState.Process -and $Restart) {
            Stop-DevelopmentApi -State $apiState -RequestedPort $Port
        }
        Add-Type -AssemblyName System.Data
        $connection = New-Object System.Data.Common.DbConnectionStringBuilder
        $connection['Host'] = 'localhost'
        $connection['Port'] = '5432'
        $connection['Database'] = $values['POSTGRES_DB']
        $connection['Username'] = $values['POSTGRES_USER']
        $connection['Password'] = $values['POSTGRES_PASSWORD']

        $processEnvironment = @{
            'POSTGRES_DB' = $values['POSTGRES_DB']
            'POSTGRES_USER' = $values['POSTGRES_USER']
            'POSTGRES_PASSWORD' = $values['POSTGRES_PASSWORD']
            'JWT_KEY' = $values['JWT_KEY']
            'ConnectionStrings__PostgreSQL' = $connection.ConnectionString
            'Jwt__Key' = $values['JWT_KEY']
            'ASPNETCORE_ENVIRONMENT' = 'Development'
        }
        foreach ($name in $processEnvironment.Keys) {
            $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
            [Environment]::SetEnvironmentVariable($name, $processEnvironment[$name], 'Process')
        }

        if ($Action -eq 'start') {
            $docker = (Get-Command docker -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
            Invoke-NativeCommand -FilePath $docker -CommandArguments @('compose', 'up', '-d', '--wait', 'postgres', 'mailpit') -Step 'Iniciando PostgreSQL y la bandeja local de correo...'
            Write-Host 'Correo local de desarrollo: http://localhost:8025 (no envia correos reales).'
        }

        Invoke-NativeCommand -FilePath $dotnet -CommandArguments @('tool', 'restore') -Step 'Restaurando herramientas locales...'
        Invoke-NativeCommand -FilePath $dotnet -CommandArguments @('build', 'src/SecurityCenterAI.Api', '--configuration', $Configuration, '--nologo') -Step 'Compilando la API...'
        Invoke-NativeCommand -FilePath $dotnet -CommandArguments @('ef', 'database', 'update', '--project', 'src/SecurityCenterAI.Infrastructure', '--startup-project', 'src/SecurityCenterAI.Api', '--configuration', $Configuration, '--no-build') -Step 'Aplicando migraciones...'

        if ($Action -eq 'start') {
            Write-Host "Swagger: http://localhost:$Port/swagger (Ctrl+C para detener la API)."
            Invoke-NativeCommand -FilePath $dotnet -CommandArguments @('run', '--project', 'src/SecurityCenterAI.Api', '--launch-profile', 'http', '--configuration', $Configuration, '--no-build', '--', '--urls', "http://localhost:$Port") -Step 'Iniciando API...'
        }
    }
}
finally {
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
    Pop-Location
}
