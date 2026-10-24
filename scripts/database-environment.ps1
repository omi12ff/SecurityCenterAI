Set-StrictMode -Version Latest

function Read-LocalPostgresEnvironment {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw 'Missing .env database configuration.' }
    $names = @('POSTGRES_DB', 'POSTGRES_USER', 'POSTGRES_PASSWORD')
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
        $entry = $line.Trim()
        if ($entry.Length -eq 0 -or $entry.StartsWith('#')) { continue }
        $separator = $entry.IndexOf('=')
        if ($separator -lt 1) { throw 'Invalid .env entry.' }
        $name = $entry.Substring(0, $separator).Trim()
        if ($names -notcontains $name) { continue }
        if ($values.ContainsKey($name)) { throw 'Duplicate database setting.' }
        $value = $entry.Substring($separator + 1).Trim()
        if ($value.StartsWith('"') -or $value.StartsWith("'")) {
            $quote = $value.Substring(0, 1)
            if ($value.Length -lt 2 -or -not $value.EndsWith($quote)) { throw 'Invalid quoted database setting.' }
            $value = $value.Substring(1, $value.Length - 2)
        }
        $values[$name] = $value
    }
    foreach ($name in $names) { if ([string]::IsNullOrWhiteSpace($values[$name])) { throw 'Missing database setting.' } }
    return $values
}
