[CmdletBinding()]
param(
    [string]$OutputDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\agent'
}
$projectPath = Join-Path $repositoryRoot 'src\SecurityCenterAI.Agent\SecurityCenterAI.Agent.csproj'
$templateDirectory = Join-Path $repositoryRoot 'packaging\agent'
$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
$workDirectory = Join-Path $temporaryRoot ('SecurityCenterAI-agent-package-' + [guid]::NewGuid().ToString('N'))
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$utf8 = New-Object System.Text.UTF8Encoding($false)
$createdOutputs = New-Object 'System.Collections.Generic.List[string]'

function Get-HexSha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

try {
    # Consultar las propiedades evaluadas también respeta un eventual Directory.Build.props.
    $metadataText = & dotnet msbuild $projectPath -nologo -getProperty:Version -getProperty:TargetFramework `
        -getProperty:NuGetPackageRoot -getProperty:ProjectAssetsFile
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron consultar las propiedades del proyecto del agente.' }
    $metadata = ($metadataText -join [Environment]::NewLine) | ConvertFrom-Json
    $version = [string]$metadata.Properties.Version
    $targetFramework = [string]$metadata.Properties.TargetFramework
    if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
        throw 'La version del agente no es una version SemVer compatible con el nombre del paquete.'
    }
    if ($targetFramework -ne 'net8.0-windows') { throw 'El empaquetado espera el agente net8.0-windows.' }

    $packageName = "SecurityCenterAI.Agent-$version-win-x64"
    $archivePath = Join-Path $outputPath ($packageName + '.zip')
    $checksumPath = $archivePath + '.sha256'
    if ((Test-Path -LiteralPath $archivePath) -or (Test-Path -LiteralPath $checksumPath)) {
        throw 'Ya existe el ZIP o su checksum. Elegi otro OutputDirectory; no se sobrescriben entregas.'
    }

    [void][System.IO.Directory]::CreateDirectory($workDirectory)
    $publishDirectory = Join-Path $workDirectory 'publish'
    $packageDirectory = Join-Path $workDirectory $packageName
    [void][System.IO.Directory]::CreateDirectory($packageDirectory)

    Write-Host "Publicando agente $version para Windows x64 con runtime incluido..."
    & dotnet publish $projectPath --configuration Release --runtime win-x64 --self-contained true `
        --output $publishDirectory -p:PublishSingleFile=false -p:PublishTrimmed=false `
        -p:DebugSymbols=false -p:DebugType=None -p:IncludeSourceRevisionInInformationalVersion=false
    if ($LASTEXITCODE -ne 0) { throw 'Fallo dotnet publish; no se genero la entrega.' }

    # Solo se admiten binarios publicados y los dos JSON del runtime. Nunca se copia el repositorio.
    foreach ($item in Get-ChildItem -LiteralPath $publishDirectory -Force -Recurse) {
        if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw 'La publicacion contiene un enlace; se cancela el empaquetado.'
        }
        if ($item.PSIsContainer) { throw 'La publicacion contiene un subdirectorio no previsto.' }
        $allowed = $item.Extension -in @('.dll', '.exe') -or
            $item.Name -in @('SecurityCenterAI.Agent.deps.json', 'SecurityCenterAI.Agent.runtimeconfig.json')
        if (-not $allowed) { throw "Archivo de publicacion no previsto: $($item.Name)" }
        [System.IO.File]::Copy($item.FullName, (Join-Path $packageDirectory $item.Name), $false)
    }
    foreach ($required in @('SecurityCenterAI.Agent.exe', 'SecurityCenterAI.Agent.dll', 'coreclr.dll',
        'hostfxr.dll', 'hostpolicy.dll', 'System.Private.CoreLib.dll',
        'SecurityCenterAI.Agent.deps.json', 'SecurityCenterAI.Agent.runtimeconfig.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageDirectory $required) -PathType Leaf)) {
            throw "Falta un archivo necesario del paquete autonomo: $required"
        }
    }
    foreach ($name in @('Iniciar-agente.cmd', 'Invoke-Agent.ps1', 'LEEME.txt')) {
        [System.IO.File]::Copy((Join-Path $templateDirectory $name), (Join-Path $packageDirectory $name), $false)
    }

    $runtimeConfiguration = Get-Content -LiteralPath (Join-Path $packageDirectory 'SecurityCenterAI.Agent.runtimeconfig.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $runtimeFramework = @($runtimeConfiguration.runtimeOptions.includedFrameworks |
        Where-Object { $_.name -eq 'Microsoft.NETCore.App' })
    if ($runtimeFramework.Count -ne 1) { throw 'No se pudo identificar el runtime incluido.' }
    $runtimeVersion = [string]$runtimeFramework[0].version
    if ($runtimeVersion -notmatch '^8\.0\.\d+$') { throw 'La version del runtime incluido no es .NET 8.' }

    # Las licencias pertenecen al runtime distribuido; no definen una licencia para el proyecto.
    $packageFolders = New-Object 'System.Collections.Generic.List[string]'
    if (-not [string]::IsNullOrWhiteSpace([string]$metadata.Properties.NuGetPackageRoot)) {
        $packageFolders.Add([string]$metadata.Properties.NuGetPackageRoot)
    }
    $assetsPath = [string]$metadata.Properties.ProjectAssetsFile
    if (-not [string]::IsNullOrWhiteSpace($assetsPath) -and (Test-Path -LiteralPath $assetsPath -PathType Leaf)) {
        $assets = Get-Content -LiteralPath $assetsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) { $packageFolders.Add([string]$folder) }
    }
    $runtimePackageDirectory = $null
    foreach ($folder in @($packageFolders | Select-Object -Unique)) {
        $candidate = Join-Path $folder ("microsoft.netcore.app.runtime.win-x64\$runtimeVersion")
        if ((Test-Path -LiteralPath (Join-Path $candidate 'LICENSE.TXT') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $candidate 'THIRD-PARTY-NOTICES.TXT') -PathType Leaf)) {
            $runtimePackageDirectory = $candidate
            break
        }
    }
    if ($null -eq $runtimePackageDirectory) {
        throw "Faltan LICENSE.TXT y THIRD-PARTY-NOTICES.TXT del runtime win-x64 $runtimeVersion en la cache NuGet. No se genera una entrega incompleta."
    }
    $runtimeNotices = @{
        'LICENSE.TXT' = 'DOTNET-LICENSE.txt'
        'THIRD-PARTY-NOTICES.TXT' = 'DOTNET-THIRD-PARTY-NOTICES.txt'
    }
    foreach ($sourceName in $runtimeNotices.Keys) {
        $sourceFile = Get-Item -LiteralPath (Join-Path $runtimePackageDirectory $sourceName)
        if ($sourceFile.Length -eq 0 -or ($sourceFile.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw 'Un archivo de licencia del runtime esta vacio o es un enlace.'
        }
        [System.IO.File]::Copy($sourceFile.FullName, (Join-Path $packageDirectory $runtimeNotices[$sourceName]), $false)
    }
    $manifestFiles = @(Get-ChildItem -LiteralPath $packageDirectory -File | Sort-Object Name | ForEach-Object {
        [ordered]@{ path = $_.Name; size = $_.Length; sha256 = (Get-HexSha256 $_.FullName) }
    })
    $manifest = [ordered]@{
        manifestVersion = 1
        packageName = $packageName
        agentVersion = $version
        targetFramework = $targetFramework
        runtimeIdentifier = 'win-x64'
        runtimeVersion = $runtimeVersion
        selfContained = $true
        singleFile = $false
        trimmed = $false
        createdAtUtc = [DateTime]::UtcNow.ToString('o')
        entryPoint = 'SecurityCenterAI.Agent.exe'
        files = $manifestFiles
    }
    [System.IO.File]::WriteAllText((Join-Path $packageDirectory 'package-manifest.json'),
        ($manifest | ConvertTo-Json -Depth 5), $utf8)

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $stagedArchive = Join-Path $workDirectory ($packageName + '.zip')
    # .NET Framework de PowerShell 5.1 usa barras invertidas en CreateFromDirectory.
    # Crear cada entrada con '/' mantiene el ZIP interoperable y las rutas verificables.
    $zipStream = [System.IO.File]::Open($stagedArchive, [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $zip = New-Object System.IO.Compression.ZipArchive($zipStream, [System.IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($file in Get-ChildItem -LiteralPath $packageDirectory -File | Sort-Object Name) {
                [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName,
                    ($packageName + '/' + $file.Name), [System.IO.Compression.CompressionLevel]::Optimal)
            }
        }
        finally { $zip.Dispose() }
    }
    finally { $zipStream.Dispose() }
    $archiveHash = Get-HexSha256 $stagedArchive
    $stagedChecksum = $stagedArchive + '.sha256'
    [System.IO.File]::WriteAllText($stagedChecksum,
        ($archiveHash + '  ' + [System.IO.Path]::GetFileName($archivePath) + [Environment]::NewLine), $utf8)

    [void][System.IO.Directory]::CreateDirectory($outputPath)
    [System.IO.File]::Copy($stagedArchive, $archivePath, $false)
    $createdOutputs.Add($archivePath)
    [System.IO.File]::Copy($stagedChecksum, $checksumPath, $false)
    $createdOutputs.Add($checksumPath)
    Write-Host "ZIP: $archivePath"
    Write-Host "SHA256: $archiveHash"
    Write-Host "Checksum: $checksumPath"
    Write-Host 'Entrega portatil creada. No instala el agente ni lo ejecuta.'
    [pscustomobject]@{ ArchivePath = $archivePath; ChecksumPath = $checksumPath; Sha256 = $archiveHash; Version = $version }
}
catch {
    foreach ($createdOutput in $createdOutputs) {
        # Solo archivos creados por esta ejecucion; nunca se elimina una entrega preexistente.
        if (Test-Path -LiteralPath $createdOutput -PathType Leaf) { Remove-Item -LiteralPath $createdOutput -Force }
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $workDirectory -PathType Container) {
        $resolvedWork = (Resolve-Path -LiteralPath $workDirectory).ProviderPath.TrimEnd('\', '/')
        $expectedPrefix = $temporaryRoot + [System.IO.Path]::DirectorySeparatorChar
        if (-not $resolvedWork.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase) -or
            [System.IO.Path]::GetFileName($resolvedWork) -notmatch '^SecurityCenterAI-agent-package-[a-f0-9]{32}$') {
            throw 'No se pudo verificar el directorio temporal para su limpieza.'
        }
        Remove-Item -LiteralPath $resolvedWork -Recurse -Force
    }
}
