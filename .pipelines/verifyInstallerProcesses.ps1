# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

#requires -Version 7.0

[CmdletBinding()]
param(
    [string] $RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [string] $CmdPalPackagePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-LeafName([string] $Path)
{
    return ($Path -replace '\\', '/').Split('/')[-1]
}

function Test-ExecutablePattern([string] $Pattern)
{
    $extension = [IO.Path]::GetExtension((Get-LeafName $Pattern))
    return $extension -eq '.exe' -or
        ([Management.Automation.WildcardPattern]::ContainsWildcardCharacters($extension) -and
            [Management.Automation.WildcardPattern]::new($extension, 'IgnoreCase').IsMatch('.exe')) -or
        ($extension.Length -eq 0 -and [Management.Automation.WildcardPattern]::ContainsWildcardCharacters($Pattern))
}

function Get-SigningPatterns([string] $Path)
{
    $manifest = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
    if ($manifest.UseMinimatch -ne $false -or @($manifest.SignBatches).Count -eq 0)
    {
        throw "Expected nonempty SignBatches and UseMinimatch=false in $Path."
    }

    foreach ($batch in $manifest.SignBatches)
    {
        if (@($batch.MatchedPath).Count -eq 0)
        {
            throw "Empty MatchedPath in $Path."
        }
        foreach ($pattern in $batch.MatchedPath)
        {
            if ($pattern -isnot [string] -or [string]::IsNullOrWhiteSpace($pattern))
            {
                throw "Invalid MatchedPath in $Path."
            }
            $pattern -replace '\\', '/'
        }
    }
}

$customActionPath = Join-Path $RepoRoot 'installer/PowerToysSetupCustomActionsVNext/CustomAction.cpp'
$source = Get-Content -LiteralPath $customActionPath -Raw
$source = [regex]::Replace($source, '(?s)/\*.*?\*/|//[^\r\n]*', '')
$initializers = [regex]::Matches($source, '(?s)\bprocessesToTerminate\s*(?:\[[^\]]*\])?\s*=\s*\{(?<body>.*?)\}\s*;')
if ($initializers.Count -ne 1)
{
    throw 'Expected exactly one processesToTerminate initializer in CustomAction.cpp.'
}

$body = $initializers[0].Groups['body'].Value
$namePattern = 'L"(?<name>[A-Za-z0-9_. -]+\.(?i:exe))"'
if ($body -cnotmatch "^\s*$namePattern\s*(?:,\s*$namePattern\s*)*,?\s*$")
{
    throw 'processesToTerminate must be a nonempty list of literal executable leaf names.'
}
$killNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($literal in [regex]::Matches($body, $namePattern))
{
    $null = $killNames.Add($literal.Groups['name'].Value)
}

$corePatterns = @(Get-SigningPatterns (Join-Path $RepoRoot '.pipelines/ESRPSigning_core.json'))
foreach ($policy in Get-ChildItem -LiteralPath (Join-Path $RepoRoot '.pipelines') -Filter 'ESRPSigning_*.json' -File)
{
    if ($policy.Name -in @('ESRPSigning_core.json', 'ESRPSigning_cmdpal_msix_content.json'))
    {
        continue
    }
    foreach ($pattern in Get-SigningPatterns $policy.FullName)
    {
        if (Test-ExecutablePattern $pattern)
        {
            throw "Unsupported executable signing policy $($policy.Name): $pattern. Add installer process coverage handling for this policy."
        }
    }
}
$corePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($pattern in $corePatterns)
{
    if (-not (Test-ExecutablePattern $pattern))
    {
        continue
    }
    if ([Management.Automation.WildcardPattern]::ContainsWildcardCharacters($pattern))
    {
        throw "Unsupported executable glob in ESRPSigning_core.json: $pattern. List executable paths explicitly."
    }
    $null = $corePaths.Add($pattern.TrimStart('/'))
}
if ($corePaths.Count -eq 0)
{
    throw 'ESRPSigning_core.json contains no executable paths.'
}

# Resolve signing-before-rename: the CLI shim payload is installed under four command names.
# Root and WinUI3Apps files are also harvested, so their original names remain required.
$installedNames = @{}
$required = [Collections.Generic.List[object]]::new()
foreach ($wxsPath in Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'installer/PowerToysSetupVNext') -Filter '*.wxs' -Recurse -File)
{
    $wxs = [xml](Get-Content -LiteralPath $wxsPath.FullName -Raw)
    foreach ($file in $wxs.SelectNodes("//*[local-name()='File']"))
    {
        # Explicit executable installation names must be covered even when WiX inherits
        # FileSource or uses a relative Source that this static checker cannot resolve.
        $name = $file.GetAttribute('Name')
        $installedNamePattern = $name -replace '\$\([^)]*\)', '*'
        if ((Test-ExecutablePattern $installedNamePattern) -and
            ($name -match '\$\(' -or [Management.Automation.WildcardPattern]::ContainsWildcardCharacters($name)))
        {
            throw "Unsupported installed File/@Name '$name' in $($wxsPath.Name). Use a literal name for coverage verification."
        }
        if ($name -match '\.exe$')
        {
            if ($name -notmatch '^[A-Za-z0-9_. -]+\.exe$')
            {
                throw "Unsupported installed executable name '$name' in $($wxsPath.Name)."
            }
            $required.Add(@{ Scope = 'core'; Path = $name; Name = $name; Source = "$($wxsPath.Name) File/@Name"; IsAlias = $true })
        }
        $sourcePath = $file.GetAttribute('Source') -replace '\\', '/'
        if (-not $sourcePath.StartsWith('$(var.BinDir)', [StringComparison]::Ordinal))
        {
            continue
        }
        $sourcePath = $sourcePath.Substring('$(var.BinDir)'.Length).TrimStart('/')
        if (-not $corePaths.Contains($sourcePath))
        {
            continue
        }
        if ([string]::IsNullOrEmpty($name))
        {
            $name = Get-LeafName $sourcePath
        }
        if ($name -notmatch '^[A-Za-z0-9_. -]+\.exe$')
        {
            throw "Unsupported installed executable name '$name' in $($wxsPath.Name)."
        }
        if (-not $installedNames.ContainsKey($sourcePath))
        {
            $installedNames[$sourcePath] = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        }
        $null = $installedNames[$sourcePath].Add($name)
    }
}

foreach ($path in $corePaths)
{
    $harvested = $path -notmatch '/' -or $path -match '^WinUI3Apps/[^/]+$'
    if ($harvested -or -not $installedNames.ContainsKey($path))
    {
        $required.Add(@{ Scope = 'core'; Path = $path; Name = (Get-LeafName $path); Source = $path; IsAlias = $false })
    }
    if ($installedNames.ContainsKey($path))
    {
        foreach ($name in $installedNames[$path])
        {
            $required.Add(@{ Scope = 'core'; Path = $path; Name = $name; Source = "$path (WiX installed name)"; IsAlias = ($name -ne (Get-LeafName $path)) })
        }
    }
}

if ($CmdPalPackagePath)
{
    $cmdPalPatterns = @(Get-SigningPatterns (Join-Path $RepoRoot '.pipelines/ESRPSigning_cmdpal_msix_content.json'))
    $matchers = @($cmdPalPatterns | ForEach-Object { [Management.Automation.WildcardPattern]::new($_, 'IgnoreCase') })
    $package = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $CmdPalPackagePath).Path)
    $signedExeCount = 0
    try
    {
        foreach ($entry in $package.Entries)
        {
            $entryPath = $entry.FullName -replace '\\', '/'
            if ($entryPath -notmatch '\.exe$')
            {
                continue
            }
            foreach ($matcher in $matchers)
            {
                if ($matcher.IsMatch($entryPath))
                {
                    $required.Add(@{ Scope = 'cmdpal'; Path = $entryPath; Name = (Get-LeafName $entryPath); Source = $entryPath; IsAlias = $false })
                    $signedExeCount++
                    break
                }
            }
        }
    }
    finally
    {
        $package.Dispose()
    }
    if ($signedExeCount -eq 0)
    {
        throw 'The CmdPal package contains no executables matched by its signing manifest.'
    }
}

# Exceptions use exact relative signing paths and a manifest scope. Renamed installation
# aliases always need kill coverage. Optional CmdPal dependencies can vary with AOT/architecture.
$exceptions = @{}
$exceptionsPath = Join-Path $RepoRoot '.pipelines/installerProcessExclusions.json'
if (Test-Path -LiteralPath $exceptionsPath)
{
    foreach ($exception in @(Get-Content -LiteralPath $exceptionsPath -Raw | ConvertFrom-Json -AsHashtable))
    {
        if ($exception.Scope -notin @('core', 'cmdpal') -or
            $exception.Path -isnot [string] -or
            $exception.Path -notmatch '^[A-Za-z0-9_. -]+(?:[/\\][A-Za-z0-9_. -]+)*\.exe$' -or
            $exception.Path -match '(^|[/\\])\.\.?([/\\]|$)' -or
            $exception.Reason -isnot [string] -or [string]::IsNullOrWhiteSpace($exception.Reason))
        {
            throw 'Each installer process exclusion requires a core/cmdpal Scope, an exact relative executable Path and a nonempty Reason.'
        }
        $optional = $exception.ContainsKey('Optional') -and $exception.Optional -eq $true
        if ($exception.ContainsKey('Optional') -and ($exception.Optional -isnot [bool] -or $exception.Scope -ne 'cmdpal'))
        {
            throw 'Optional exclusions are supported only for CmdPal package dependencies.'
        }
        $path = $exception.Path -replace '\\', '/'
        $key = "$($exception.Scope)/$path"
        if ($exceptions.ContainsKey($key))
        {
            throw "Duplicate installer process exclusion: $key."
        }
        $exceptions[$key] = $exception.Reason
        if ($exception.Scope -eq 'core' -or $CmdPalPackagePath)
        {
            $present = @($required | Where-Object { $_.Scope -eq $exception.Scope -and $_.Path -eq $path -and -not $_.IsAlias }).Count -gt 0
            if ((-not $present -and -not $optional) -or $killNames.Contains((Get-LeafName $path)))
            {
                throw "Stale installer process exclusion: $key. Remove it or update its documented reason."
            }
        }
    }
}

$missing = @($required | Where-Object {
        -not $killNames.Contains($_.Name) -and ($_.IsAlias -or -not $exceptions.ContainsKey("$($_.Scope)/$($_.Path)"))
    } | Sort-Object Scope, Name -Unique)
if ($missing.Count -gt 0)
{
    $details = $missing | ForEach-Object { "  $($_.Name) [$($_.Scope): $($_.Source)]" }
    throw "Signed installed executables missing from processesToTerminate:`n$($details -join "`n")"
}

Write-Host "Installer process coverage passed ($($corePaths.Count) core signing paths; CmdPal package checked: $([bool]$CmdPalPackagePath))."
