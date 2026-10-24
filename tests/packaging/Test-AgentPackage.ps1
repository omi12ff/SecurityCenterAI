[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,
    [Alias('RunVersion')]
    [switch]$RunDiagnostics
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archivePathResolved = (Resolve-Path -LiteralPath $ArchivePath).ProviderPath
$checksumPath = $archivePathResolved + '.sha256'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$checks = 0

function Assert-Package([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}

function Get-EntryText($Entry) {
    $reader = New-Object System.IO.StreamReader($Entry.Open(), $utf8)
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Get-EntryHash($Entry) {
    $stream = $Entry.Open()
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $sha.Dispose() }
}

Assert-Package (Test-Path -LiteralPath $checksumPath -PathType Leaf) 'Falta el checksum del ZIP.'
$checksum = (Get-Content -LiteralPath $checksumPath -Raw -Encoding UTF8).Trim()
$expectedChecksumLine = (Get-FileHash -LiteralPath $archivePathResolved -Algorithm SHA256).Hash.ToLowerInvariant() +
    '  ' + [System.IO.Path]::GetFileName($archivePathResolved)
Assert-Package ($checksum -ceq $expectedChecksumLine) 'El SHA256 externo no coincide con el ZIP.'

$archive = [System.IO.Compression.ZipFile]::OpenRead($archivePathResolved)
try {
    $entries = @($archive.Entries)
    $manifestEntries = @($entries | Where-Object { $_.FullName -match '^[^/]+/package-manifest\.json$' })
    Assert-Package ($manifestEntries.Count -eq 1) 'Debe existir un solo manifiesto bajo la carpeta del paquete.'
    $manifest = (Get-EntryText $manifestEntries[0]) | ConvertFrom-Json
    Assert-Package ($manifest.manifestVersion -eq 1) 'Version de manifiesto no compatible.'
    Assert-Package ($manifest.agentVersion -match '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') 'Version del agente invalida.'
    Assert-Package ($manifest.packageName -ceq ('SecurityCenterAI.Agent-' + $manifest.agentVersion + '-win-x64')) 'Nombre de paquete invalido.'
    Assert-Package ([System.IO.Path]::GetFileName($archivePathResolved) -ceq ($manifest.packageName + '.zip')) 'El nombre del ZIP no coincide con el manifiesto.'
    Assert-Package ($manifest.targetFramework -ceq 'net8.0-windows') 'Framework no previsto.'
    Assert-Package ($manifest.runtimeIdentifier -ceq 'win-x64') 'Runtime no previsto.'
    Assert-Package ($manifest.runtimeVersion -match '^8\.0\.\d+$') 'No se identifico un runtime .NET 8 incluido.'
    Assert-Package ($manifest.selfContained -eq $true -and $manifest.singleFile -eq $false -and $manifest.trimmed -eq $false) 'La publicacion debe incluir runtime sin single-file ni trimming.'
    Assert-Package ($manifest.entryPoint -ceq 'SecurityCenterAI.Agent.exe') 'Punto de entrada no previsto.'

    $prefix = $manifest.packageName + '/'
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $entries) {
        Assert-Package ($entry.FullName.StartsWith($prefix, [StringComparison]::Ordinal) -and
            $entry.FullName -notmatch '\\|(^|/)\.\.(/|$)|(^|/)\.(/|$)|:' -and $entry.Name -ne '') 'Entrada fuera de la carpeta del paquete o ruta no segura.'
        $relative = $entry.FullName.Substring($prefix.Length)
        Assert-Package ($relative -notmatch '/') 'No se esperan subcarpetas en el paquete.'
        Assert-Package ($seen.Add($relative)) 'El ZIP contiene entradas duplicadas.'
        $allowed = $relative -match '\.(dll|exe)$' -or $relative -cin @('SecurityCenterAI.Agent.deps.json',
            'SecurityCenterAI.Agent.runtimeconfig.json', 'Iniciar-agente.cmd', 'Invoke-Agent.ps1', 'LEEME.txt',
            'DOTNET-LICENSE.txt', 'DOTNET-THIRD-PARTY-NOTICES.txt', 'package-manifest.json')
        Assert-Package $allowed 'El ZIP contiene archivos ajenos a la distribucion del agente.'
    }
    $manifestFiles = @($manifest.files)
    Assert-Package ($manifestFiles.Count -eq $entries.Count - 1) 'La cantidad de archivos del manifiesto no coincide.'
    $declared = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $manifestFiles) {
        Assert-Package ($declared.Add([string]$file.path) -and $file.path -cne 'package-manifest.json') 'Entrada duplicada o autorreferencia en el manifiesto.'
        $entry = $archive.GetEntry($prefix + $file.path)
        Assert-Package ($null -ne $entry) 'Falta un archivo declarado en el manifiesto.'
        Assert-Package ($entry.Length -eq $file.size) 'El tamano de un archivo no coincide.'
        Assert-Package ($file.sha256 -cmatch '^[a-f0-9]{64}$' -and (Get-EntryHash $entry) -ceq $file.sha256) 'El SHA256 de un archivo no coincide.'
    }
    foreach ($required in @('SecurityCenterAI.Agent.exe', 'SecurityCenterAI.Agent.dll', 'coreclr.dll',
        'hostfxr.dll', 'hostpolicy.dll', 'System.Private.CoreLib.dll',
        'SecurityCenterAI.Agent.deps.json', 'SecurityCenterAI.Agent.runtimeconfig.json',
        'Iniciar-agente.cmd', 'Invoke-Agent.ps1', 'LEEME.txt', 'DOTNET-LICENSE.txt', 'DOTNET-THIRD-PARTY-NOTICES.txt')) {
        Assert-Package ($null -ne $archive.GetEntry($prefix + $required)) 'Falta un componente obligatorio.'
    }
    foreach ($runtimeNotice in @('DOTNET-LICENSE.txt', 'DOTNET-THIRD-PARTY-NOTICES.txt')) {
        Assert-Package ($archive.GetEntry($prefix + $runtimeNotice).Length -gt 0) 'Un archivo de licencia del runtime esta vacio.'
    }
    $runtime = (Get-EntryText ($archive.GetEntry($prefix + 'SecurityCenterAI.Agent.runtimeconfig.json'))) | ConvertFrom-Json
    $frameworks = @($runtime.runtimeOptions.includedFrameworks | Where-Object { $_.name -ceq 'Microsoft.NETCore.App' })
    Assert-Package ($frameworks.Count -eq 1 -and $frameworks[0].version -ceq $manifest.runtimeVersion) 'El runtime real no coincide con el manifiesto.'
    foreach ($launcher in @('Iniciar-agente.cmd', 'Invoke-Agent.ps1')) {
        $launcherText = Get-EntryText ($archive.GetEntry($prefix + $launcher))
        Assert-Package ($launcherText -notmatch '(?i)Set-ExecutionPolicy|ExecutionPolicy\s+Bypass|dotnet\s+run|Start-Service|schtasks') 'El launcher contiene acciones no admitidas.'
    }
}
finally { $archive.Dispose() }

if ($RunDiagnostics) {
    $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
    $testDirectory = Join-Path $temporaryRoot ('SecurityCenterAI-agent-package-test-' + [guid]::NewGuid().ToString('N'))
    try {
        # Ruta con espacios y caracteres no ASCII: no depender del directorio original del ZIP.
        $unicodeDirectory = Join-Path $testDirectory ('PC de prueba ' + [char]0x00F1)
        [void][System.IO.Directory]::CreateDirectory($unicodeDirectory)
        [System.IO.Compression.ZipFile]::ExtractToDirectory($archivePathResolved, $unicodeDirectory)
        $executable = Join-Path (Join-Path $unicodeDirectory $manifest.packageName) 'SecurityCenterAI.Agent.exe'
        $packagePath = Split-Path -Parent $executable
        $systemDirectory = Join-Path $env:SystemRoot 'System32'
        $powerShellPath = Join-Path $systemDirectory 'WindowsPowerShell\v1.0\powershell.exe'
        $processCases = @(
            @{ file = $executable; arguments = '--version'; kind = 'version' },
            @{ file = $executable; arguments = 'version'; kind = 'version' },
            @{ file = $executable; arguments = 'help'; kind = 'help' },
            @{ file = $executable; arguments = '--help'; kind = 'help' },
            @{ file = $executable; arguments = 'doctor'; kind = 'doctor' },
            @{ file = $executable; arguments = 'doctor --json'; kind = 'doctor-json' },
            @{ file = (Join-Path $systemDirectory 'cmd.exe');
                arguments = ('/d /c ""' + (Join-Path $packagePath 'Iniciar-agente.cmd') + '" version"'); kind = 'version' },
            @{ file = $powerShellPath;
                arguments = ('-NoProfile -File "' + (Join-Path $packagePath 'Invoke-Agent.ps1') + '" -Action version'); kind = 'version' },
            @{ file = $powerShellPath;
                arguments = ('-NoProfile -File "' + (Join-Path $packagePath 'Invoke-Agent.ps1') + '" -Action scan -Replace'); kind = 'invalid-options' }
        )
        foreach ($processCase in $processCases) {
            $startInfo = New-Object System.Diagnostics.ProcessStartInfo
            $startInfo.FileName = $processCase.file
            $startInfo.Arguments = $processCase.arguments
            $startInfo.UseShellExecute = $false
            $startInfo.CreateNoWindow = $true
            $startInfo.RedirectStandardOutput = $true
            $startInfo.RedirectStandardError = $true
            $startInfo.StandardOutputEncoding = $utf8
            $startInfo.StandardErrorEncoding = $utf8
            $startInfo.EnvironmentVariables['PATH'] = $systemDirectory
            $startInfo.EnvironmentVariables['DOTNET_ROOT'] = Join-Path $testDirectory 'runtime-no-instalado'
            $startInfo.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
            $process = [System.Diagnostics.Process]::Start($startInfo)
            try {
                $outputTask = $process.StandardOutput.ReadToEndAsync()
                $errorTask = $process.StandardError.ReadToEndAsync()
                if (-not $process.WaitForExit(15000)) { $process.Kill(); throw 'La consulta del agente no termino.' }
                $output = $outputTask.GetAwaiter().GetResult()
                $errorOutput = $errorTask.GetAwaiter().GetResult()
                if ($processCase.kind -eq 'invalid-options') {
                    Assert-Package ($process.ExitCode -eq 1 -and $errorOutput.Contains('solo se admiten para pair')) 'El wrapper no rechazo opciones incompatibles antes de ejecutar el agente.'
                    continue
                }
                Assert-Package ([string]::IsNullOrWhiteSpace($errorOutput)) 'Una consulta del agente produjo errores.'
                if ($processCase.kind -like 'doctor*') {
                    Assert-Package ($process.ExitCode -in @(0, 2)) 'El diagnostico devolvio un codigo inesperado.'
                    if ($processCase.kind -eq 'doctor-json') {
                        $diagnostic = $output | ConvertFrom-Json
                        Assert-Package ($diagnostic.agentVersion -ceq $manifest.agentVersion) 'Version inconsistente en doctor.'
                        Assert-Package ($diagnostic.runtimeVersion -ceq ('.NET ' + $manifest.runtimeVersion)) 'Doctor no esta usando el runtime incluido.'
                        Assert-Package ($diagnostic.processArchitecture -ceq 'X64') 'El proceso publicado no es x64.'
                        Assert-Package ($diagnostic.checks.Count -eq 5 -and $diagnostic.limits.Count -ge 3) 'El diagnostico no incluye requisitos y limites.'
                        Assert-Package ($process.ExitCode -eq $(if ($diagnostic.basicRequirementsAvailable) { 0 } else { 2 })) 'El estado del diagnostico no coincide con su codigo de salida.'
                    }
                }
                else {
                    Assert-Package ($process.ExitCode -eq 0) 'El ejecutable o launcher no pudo mostrar ayuda/version.'
                    Assert-Package ($output.Contains([string]$manifest.agentVersion)) 'La version ejecutada no coincide con el manifiesto.'
                }
            }
            finally { $process.Dispose() }
        }
    }
    finally {
        if (Test-Path -LiteralPath $testDirectory -PathType Container) {
            $resolvedTest = (Resolve-Path -LiteralPath $testDirectory).ProviderPath.TrimEnd('\', '/')
            if (-not $resolvedTest.StartsWith(($temporaryRoot + [System.IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase) -or
                [System.IO.Path]::GetFileName($resolvedTest) -notmatch '^SecurityCenterAI-agent-package-test-[a-f0-9]{32}$') {
                throw 'No se pudo verificar la carpeta temporal de pruebas para su limpieza.'
            }
            Remove-Item -LiteralPath $resolvedTest -Recurse -Force
        }
    }
}

Write-Host "Paquete verificado: $checks comprobaciones; archivos y SHA256 coinciden."
