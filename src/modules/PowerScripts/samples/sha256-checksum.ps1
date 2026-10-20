# Compute SHA-256. Metadata lives in the sibling sha256-checksum.ps1.tool.json descriptor (an MCP
# Tool). A "file" PowerScript surfaced in the Explorer right-click menu (contextMenu inferred).
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
