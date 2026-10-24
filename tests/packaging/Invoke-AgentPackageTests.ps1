[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$validator = Join-Path $PSScriptRoot 'Test-AgentPackage.ps1'
$sourceArchive = (Resolve-Path -LiteralPath $ArchivePath).ProviderPath
$utf8 = New-Object System.Text.UTF8Encoding($false)
$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
$testDirectory = Join-Path $temporaryRoot ('SecurityCenterAI-agent-package-cases-' + [guid]::NewGuid().ToString('N'))
$passed = 0

function Write-PackageChecksum([string]$Path) {
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    [System.IO.File]::WriteAllText(($Path + '.sha256'),
        ($hash + '  ' + [System.IO.Path]::GetFileName($Path) + [Environment]::NewLine), $utf8)
}

function Assert-Rejected([string]$Path, [string]$ExpectedMessage) {
    $wasRejected = $false
    try { & $validator -ArchivePath $Path }
    catch {
        if ($_.Exception.Message -notlike ('*' + $ExpectedMessage + '*')) { throw }
        $wasRejected = $true
    }
    if (-not $wasRejected) { throw "El paquete alterado no fue rechazado: $ExpectedMessage" }
    $script:passed++
}

try {
    & $validator -ArchivePath $sourceArchive -RunDiagnostics
    $passed++
    [void][System.IO.Directory]::CreateDirectory($testDirectory)
    foreach ($case in @('modified-file', 'unlisted-secret', 'path-traversal', 'zip-checksum')) {
        $caseDirectory = Join-Path $testDirectory $case
        [void][System.IO.Directory]::CreateDirectory($caseDirectory)
        $changedArchive = Join-Path $caseDirectory ([System.IO.Path]::GetFileName($sourceArchive))
        [System.IO.File]::Copy($sourceArchive, $changedArchive, $false)
        if ($case -eq 'zip-checksum') {
            [System.IO.File]::WriteAllText(($changedArchive + '.sha256'),
                (('0' * 64) + '  ' + [System.IO.Path]::GetFileName($changedArchive)), $utf8)
            Assert-Rejected $changedArchive 'SHA256 externo'
            continue
        }
        $archive = [System.IO.Compression.ZipFile]::Open($changedArchive, [System.IO.Compression.ZipArchiveMode]::Update)
        try {
            $manifestEntry = @($archive.Entries | Where-Object { $_.FullName -match '/package-manifest\.json$' })[0]
            $prefix = $manifestEntry.FullName.Substring(0, $manifestEntry.FullName.LastIndexOf('/') + 1)
            if ($case -eq 'modified-file') {
                $entry = $archive.GetEntry($prefix + 'Iniciar-agente.cmd')
                $entryStream = $entry.Open()
                $buffer = New-Object System.IO.MemoryStream
                try { $entryStream.CopyTo($buffer); $changedBytes = $buffer.ToArray() }
                finally { $entryStream.Dispose(); $buffer.Dispose() }
                $changedBytes[0] = [byte]($changedBytes[0] -bxor 1)
                $entry.Delete()
                $entry = $archive.CreateEntry($prefix + 'Iniciar-agente.cmd')
                $entryStream = $entry.Open()
                try { $entryStream.Write($changedBytes, 0, $changedBytes.Length) }
                finally { $entryStream.Dispose() }
            }
            else {
                if ($case -eq 'unlisted-secret') { $entry = $archive.CreateEntry($prefix + '.env') }
                else { $entry = $archive.CreateEntry($prefix + '../fuera.txt') }
                $writer = New-Object System.IO.StreamWriter($entry.Open(), $utf8)
                try { $writer.Write('contenido de prueba') } finally { $writer.Dispose() }
            }
        }
        finally { $archive.Dispose() }
        # Un hash externo recalculado no debe ocultar cambios dentro del paquete.
        Write-PackageChecksum $changedArchive
        if ($case -eq 'modified-file') { Assert-Rejected $changedArchive 'SHA256 de un archivo' }
        elseif ($case -eq 'unlisted-secret') { Assert-Rejected $changedArchive 'archivos ajenos' }
        else { Assert-Rejected $changedArchive 'ruta no segura' }
    }
    Write-Host "Pruebas de empaquetado aprobadas: $passed (incluyen ejecucion autonoma y rechazo de alteraciones)."
}
finally {
    if (Test-Path -LiteralPath $testDirectory -PathType Container) {
        $resolvedTest = (Resolve-Path -LiteralPath $testDirectory).ProviderPath.TrimEnd('\', '/')
        if (-not $resolvedTest.StartsWith(($temporaryRoot + [System.IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase) -or
            [System.IO.Path]::GetFileName($resolvedTest) -notmatch '^SecurityCenterAI-agent-package-cases-[a-f0-9]{32}$') {
            throw 'No se pudo verificar la carpeta temporal de casos para su limpieza.'
        }
        Remove-Item -LiteralPath $resolvedTest -Recurse -Force
    }
}
