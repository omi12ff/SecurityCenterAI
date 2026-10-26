[CmdletBinding()]
param(
    [ValidateSet('status', 'configure', 'disable')]
    [string]$Action = 'status',
    [string]$Model = '',
    [ValidateRange(1, 50)]
    [int]$DailyLimit = 5,
    [ValidateRange(5, 60)]
    [int]$TimeoutSeconds = 30,
    [Security.SecureString]$ApiKey,
    [switch]$ReplaceKey
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Este asistente requiere Windows y DPAPI CurrentUser.' }
Add-Type -AssemblyName System.Security
$localRoot = [Environment]::GetFolderPath('LocalApplicationData')
if ([string]::IsNullOrWhiteSpace($localRoot)) { throw 'No se encontro LocalApplicationData.' }
$privateDirectory = [IO.Path]::GetFullPath((Join-Path $localRoot 'SecurityCenterAI\Api\AiExplanation'))
$settingsPath = Join-Path $privateDirectory 'settings.json'
$keyPath = Join-Path $privateDirectory 'gateway-key.dpapi'
$entropy = [Text.Encoding]::UTF8.GetBytes('SecurityCenterAI.AiExplanation.v1')

function Assert-OrdinaryPath {
    param([string]$Target)
    $candidate = [IO.Path]::GetFullPath($Target)
    while ($candidate) {
        if (Test-Path -LiteralPath $candidate) {
            $entry = Get-Item -LiteralPath $candidate -Force
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'La configuracion no admite enlaces ni puntos de reanalisis.'
            }
        }
        $parent = [IO.Directory]::GetParent($candidate)
        if ($null -eq $parent) { break }
        $candidate = $parent.FullName
    }
}

function Protect-PrivateDirectory {
    Assert-OrdinaryPath $privateDirectory
    [void][IO.Directory]::CreateDirectory($privateDirectory)
    # Preserve owner/group metadata. A fresh empty security descriptor can make
    # Set-Acl request audit privileges on an already protected directory.
    $acl = Get-Acl -LiteralPath $privateDirectory
    $acl.SetAccessRuleProtection($true, $false)
    $ownerSid = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $systemSid = New-Object Security.Principal.SecurityIdentifier('S-1-5-18')
    foreach ($existing in @($acl.Access)) { [void]$acl.RemoveAccessRuleSpecific($existing) }
    foreach ($sid in @($ownerSid, $systemSid)) {
        $rule = New-Object Security.AccessControl.FileSystemAccessRule($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        [void]$acl.AddAccessRule($rule)
    }
    # Persist access rules directly; PowerShell Set-Acl may also request audit
    # privileges when reapplying an already protected directory descriptor.
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        [IO.FileSystemAclExtensions]::SetAccessControl([IO.DirectoryInfo]::new($privateDirectory), $acl)
    }
    else { [IO.Directory]::SetAccessControl($privateDirectory, $acl) }
}

function Write-PrivateFile {
    param([string]$Destination, [byte[]]$Bytes)
    Assert-OrdinaryPath $Destination
    $temporary = Join-Path $privateDirectory ([IO.Path]::GetFileName($Destination) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $stream = New-Object IO.FileStream($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $stream.Write($Bytes, 0, $Bytes.Length); $stream.Flush($true) }
        finally { $stream.Dispose() }
        Move-Item -LiteralPath $temporary -Destination $Destination -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporary -PathType Leaf) { Remove-Item -LiteralPath $temporary -Force }
    }
}

function Test-Token {
    param([string]$Value)
    return $Value.Length -ge 16 -and $Value.Length -le 4096 -and $Value -cmatch '^[!-~]+$'
}

function Test-FreeModel {
    param([string]$Value)
    if ([string]::IsNullOrEmpty($Value) -or $Value.Length -gt 200) { return $false }
    return $Value -ceq 'openrouter/openrouter/free' -or
        $Value -cmatch '\Aopenrouter/[a-z0-9][a-z0-9._-]*/[a-z0-9][a-z0-9._-]*:free\z'
}

function Assert-SavedKey {
    Assert-OrdinaryPath $keyPath
    $size = (Get-Item -LiteralPath $keyPath).Length
    if ($size -lt 1 -or $size -gt 16384) { throw 'La clave guardada no tiene un formato valido. Usa -ReplaceKey.' }
    $cipher = $null
    $clear = $null
    try {
        $cipher = [IO.File]::ReadAllBytes($keyPath)
        $clear = [Security.Cryptography.ProtectedData]::Unprotect($cipher, $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        $decoder = New-Object Text.UTF8Encoding($false, $true)
        $value = $decoder.GetString($clear)
        if (-not (Test-Token $value)) { throw 'Invalid key.' }
    }
    catch { throw 'No se pudo abrir la clave guardada con este usuario de Windows. Usa -ReplaceKey.' }
    finally {
        $value = $null
        if ($null -ne $clear) { [Array]::Clear($clear, 0, $clear.Length) }
        if ($null -ne $cipher) { [Array]::Clear($cipher, 0, $cipher.Length) }
    }
}

Assert-OrdinaryPath $settingsPath
Assert-OrdinaryPath $keyPath
$configuration = [ordered]@{
    Enabled = $false
    GatewayBaseUrl = 'http://127.0.0.1:20128/v1/'
    Model = ''
    ApiKeyFile = $keyPath
    TimeoutSeconds = 30
    DailyLimit = 5
}
if (Test-Path -LiteralPath $settingsPath -PathType Leaf) {
    try {
        if ((Get-Item -LiteralPath $settingsPath).Length -gt 16384) { throw 'Invalid settings.' }
        $saved = [IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json
        $section = $saved.AiExplanation
        foreach ($property in @('Enabled', 'GatewayBaseUrl', 'Model', 'TimeoutSeconds', 'DailyLimit')) {
            if ($null -ne $section.PSObject.Properties[$property]) { $configuration[$property] = $section.$property }
        }
    }
    catch { throw 'settings.json no tiene un formato valido. Revisalo antes de continuar.' }
}

if ($Action -eq 'status') {
    Write-Host ('Explicaciones con IA habilitadas: ' + [bool]$configuration.Enabled)
    Write-Host ('Modelo: ' + $(if ($configuration.Model) { $configuration.Model } else { '(sin elegir)' }))
    Write-Host ('Limite por cuenta/dia UTC: ' + $configuration.DailyLimit)
    Write-Host ('Tiempo maximo (segundos): ' + $configuration.TimeoutSeconds)
    Write-Host ('Archivo de clave local presente: ' + (Test-Path -LiteralPath $keyPath -PathType Leaf))
    Write-Host 'El estado no consulta al proveedor ni muestra o descifra claves.'
    Write-Host 'Las variables AiExplanation__... tienen prioridad y la API lee la configuracion al iniciar.'
    return
}

if ($Action -eq 'configure') {
    if (-not (Test-FreeModel $Model)) {
        throw 'Indica -Model openrouter/openrouter/free o un identificador openrouter/proveedor/modelo:free. El nombre corto openrouter/free es ambiguo. No se admiten modelos de pago, rutas auto ni alias antiguos or/.'
    }
    if ($null -ne $ApiKey -and (Test-Path -LiteralPath $keyPath -PathType Leaf) -and -not $ReplaceKey) {
        throw 'Ya existe una clave. Para sustituirla indica -ReplaceKey.'
    }
    Protect-PrivateDirectory
    if (-not (Test-Path -LiteralPath $keyPath -PathType Leaf) -or $ReplaceKey) {
        $prompted = $false
        if ($null -eq $ApiKey) {
            $ApiKey = Read-Host 'Pega la clave LOCAL de OmniRoute para la app (entrada oculta)' -AsSecureString
            $prompted = $true
        }
        $pointer = [IntPtr]::Zero
        $clear = $null
        $cipher = $null
        try {
            $pointer = [Runtime.InteropServices.Marshal]::SecureStringToGlobalAllocUnicode($ApiKey)
            $value = [Runtime.InteropServices.Marshal]::PtrToStringUni($pointer)
            if (-not (Test-Token $value)) { throw 'La clave no tiene un formato valido.' }
            $clear = [Text.Encoding]::UTF8.GetBytes($value)
            $cipher = [Security.Cryptography.ProtectedData]::Protect($clear, $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
            Write-PrivateFile $keyPath $cipher
        }
        finally {
            $value = $null
            if ($pointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeGlobalAllocUnicode($pointer) }
            if ($null -ne $clear) { [Array]::Clear($clear, 0, $clear.Length) }
            if ($null -ne $cipher) { [Array]::Clear($cipher, 0, $cipher.Length) }
            if ($prompted) { $ApiKey.Dispose() }
        }
    }
    else { Assert-SavedKey }
    $configuration.Enabled = $true
    $configuration.GatewayBaseUrl = 'http://127.0.0.1:20128/v1/'
    $configuration.Model = $Model
    $configuration.DailyLimit = $DailyLimit
    $configuration.TimeoutSeconds = $TimeoutSeconds
}
else {
    Protect-PrivateDirectory
    $configuration.Enabled = $false
}

$json = @{ AiExplanation = $configuration } | ConvertTo-Json -Depth 4
Write-PrivateFile $settingsPath ([Text.UTF8Encoding]::new($false).GetBytes($json))
Write-Host 'Configuracion local guardada fuera del repositorio. Reinicia la API para aplicarla.'
Write-Host 'No se hizo ninguna consulta de IA. Usa -Action status para consultar el estado.'
