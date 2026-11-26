#requires -Version 7.0

param(
    [Parameter(Mandatory)]
    [string] $BuildRoot,

    [Parameter(Mandatory)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$policy = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ESRPSigning_core.json') -Raw | ConvertFrom-Json
$resourcePattern = '*.resources.dll'
$resourceBatches = @($policy.SignBatches | Where-Object { $_.MatchedPath -contains $resourcePattern })
if ($policy.UseMinimatch -or $resourceBatches.Count -ne 1 -or
    @($resourceBatches[0].MatchedPath | Where-Object { $_ -eq $resourcePattern }).Count -ne 1) {
    throw 'Expected one legacy resource DLL wildcard in the core signing policy.'
}

$root = (Get-Item -LiteralPath $BuildRoot).FullName
$resources = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter $resourcePattern |
    ForEach-Object { [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/') } |
    Sort-Object)
if ($resources.Count -lt 2 -or @($resources | Where-Object { $_ -match '[\[\]{}()*?!+@]' }).Count) {
    throw 'Expected at least two resource DLLs with paths that can be used as literal minimatch patterns.'
}

$resourceSigningInfo = $resourceBatches[0].SigningInfo
$resourceBatches[0].MatchedPath = @($resourceBatches[0].MatchedPath | Where-Object { $_ -ne $resourcePattern })
if ($resourceBatches[0].MatchedPath.Count -eq 0) {
    throw 'The core signing policy has no patterns left in the resource signing group.'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$policy | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'core-binaries.json') -Encoding utf8NoBOM

$midpoint = [int][Math]::Ceiling($resources.Count / 2)
$parts = @(
    @{ Name = 'core-resources-1'; Paths = @($resources[0..($midpoint - 1)]) }
    @{ Name = 'core-resources-2'; Paths = @($resources[$midpoint..($resources.Count - 1)]) }
)
foreach ($part in $parts) {
    @{
        Version = $policy.Version
        UseMinimatch = $true
        SignBatches = @(@{
            MatchedPath = $part.Paths
            SigningInfo = $resourceSigningInfo
        })
    } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputDirectory "$($part.Name).json") -Encoding utf8NoBOM
    Write-Host "$($part.Name): $($part.Paths.Count) resource DLLs"
}
