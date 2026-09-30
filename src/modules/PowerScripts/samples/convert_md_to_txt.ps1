# Convert Markdown to Text. Metadata lives in the sibling convert_md_to_txt.ps1.tool.json descriptor
# (an MCP Tool). Its file input and .md extension filter make it available in the Explorer context
# menu. Writes a plain .txt next to each selected .md file (light Markdown stripping).

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

    $outputDirectory = Split-Path -Parent $path
    if ($env:POWERSCRIPTS_MXC_WORKSPACE) {
        $outputDirectory = $env:POWERSCRIPTS_MXC_WORKSPACE
    }

    $out = Join-Path $outputDirectory ([System.IO.Path]::GetFileNameWithoutExtension($path) + '.txt')
    Set-Content -LiteralPath $out -Value $text -Encoding UTF8 -ErrorAction Stop
    "Converted: $out"
}
