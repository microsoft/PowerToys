# TEMP is inside the per-run workspace when MXC filesystem isolation is enabled.
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$path = Join-Path $env:TEMP 'report.txt'
Set-Content -LiteralPath $path -Value "Created at $(Get-Date -Format o)" -Encoding UTF8
@{ files = @($path) } | ConvertTo-Json -Compress
