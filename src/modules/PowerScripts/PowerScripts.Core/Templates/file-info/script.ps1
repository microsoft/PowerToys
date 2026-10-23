param([string[]]$Files)

[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'

if (-not $Files) {
    $Files = @($env:POWERSCRIPTS_FILES -split "`n" | Where-Object { $_ })
}
if (-not $Files) {
    throw 'Select at least one file.'
}

foreach ($path in $Files) {
    $file = Get-Item -LiteralPath $path -ErrorAction Stop
    '{0} ({1} bytes)' -f $file.Name, $file.Length
}
