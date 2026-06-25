# Convert Markdown to Text — a "file" PowerScript surfaced on .md right-click.
# Writes a plain .txt next to each selected .md file (light Markdown stripping).

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

    $text = Get-Content -LiteralPath $path -Raw
    # Light Markdown stripping: headings, emphasis markers, inline code backticks.
    $text = $text -replace '(?m)^\s{0,3}#{1,6}\s*', ''
    $text = $text -replace '(\*\*|__|\*|_|`)', ''

    $out = [System.IO.Path]::ChangeExtension($path, '.txt')
    Set-Content -LiteralPath $out -Value $text -Encoding UTF8
    "Converted: $out"
}
