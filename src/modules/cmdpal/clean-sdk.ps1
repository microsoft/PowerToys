
$gitRoot = git rev-parse --show-toplevel
$api = Join-Path $gitRoot "x64\Debug\Microsoft.CommandPalette.Extensions"
$helpers = Join-Path $gitRoot "x64\Debug\Microsoft.CommandPalette.Extensions.Toolkit"

Remove-Item -Path $api -Recurse -Force | Out-Null
Remove-Item -Path $helpers -Recurse -Force | Out-Null
