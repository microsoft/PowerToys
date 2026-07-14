# @powerscript.id           sha256-checksum
# @powerscript.name         Compute SHA-256
# @powerscript.description   Compute the SHA-256 checksum of the selected file(s).
# @powerscript.kind         file
# @powerscript.extensions   *
# @powerscript.output       sideEffect
# @powerscript.capability   fileRead
#
# A "file" PowerScript surfaced in the Explorer right-click menu (contextMenu is inferred).
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
