# Edit the sibling script.ps1.tool.json to change the contract; see README.md.
param([string]$name = 'World', [int]$times = 1)

[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
1..$times | ForEach-Object { Write-Output "Hello, $name!" }
