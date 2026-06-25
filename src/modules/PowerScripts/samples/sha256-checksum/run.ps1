# Compute SHA-256 — a "file" PowerScript.
# Surfaced in the Explorer right-click menu for the selected file(s).
# Files arrive both as -Files and via the POWERSCRIPTS_FILES environment variable.

param(
    [string[]]$Files
)

if (-not $Files -or $Files.Count -eq 0) {
    if ($env:POWERSCRIPTS_FILES) {
        $Files = $env:POWERSCRIPTS_FILES -split "`n"
    }
}

if (-not $Files -or $Files.Count -eq 0) {
    Write-Error 'No files provided.'
    exit 1
}

foreach ($f in $Files) {
    $path = $f.Trim()
    if (-not $path) { continue }
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Warning "Not found: $path"
        continue
    }

    $hash = Get-FileHash -LiteralPath $path -Algorithm SHA256
    '{0}  {1}' -f $hash.Hash, $path
}
