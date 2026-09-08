# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0

<#
.SYNOPSIS
Verifies an isolated PowerOCR SDK comparison without restoring, building, or launching it.
.DESCRIPTION
Checks comparison.json, the package graph, inherited configuration, snapshot sources, and original files.
Project-file hashes are skipped because the isolated project is intentionally rewritten.
With RequireOutput, also records application hashes and compares native output with the SDK packages.
Writes a JSON report inside VariantRoot and returns only its absolute path on success.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$VariantRoot,
    [switch]$RequireOutput
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not [IO.Path]::IsPathFullyQualified($VariantRoot)) {
    throw 'VariantRoot must be an absolute filesystem path.'
}
$variantDirectory = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($VariantRoot))
if (-not (Test-Path -LiteralPath $variantDirectory -PathType Container)) {
    throw "VariantRoot does not exist: $variantDirectory"
}
$variantPrefix = $variantDirectory + [IO.Path]::DirectorySeparatorChar

function Assert-VariantPath([string]$Path, [string]$Description) {
    if (-not [IO.Path]::IsPathFullyQualified($Path)) {
        throw "$Description must be an absolute filesystem path."
    }
    $fullPath = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path))
    if ($fullPath -ne $variantDirectory -and -not $fullPath.StartsWith($variantPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must remain inside VariantRoot: $fullPath"
    }

    # Reject junctions/symlinks that could redirect report writes or variant reads elsewhere.
    $currentPath = $fullPath
    while ($currentPath.Length -ge $variantDirectory.Length) {
        $item = Get-Item -LiteralPath $currentPath -Force -ErrorAction SilentlyContinue
        if ($null -ne $item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Description traverses a reparse point: $currentPath"
        }
        if ($currentPath -eq $variantDirectory) { break }
        $currentPath = [IO.Path]::GetDirectoryName($currentPath)
    }
    return $fullPath
}

$manifestPath = Assert-VariantPath (Join-Path $variantDirectory 'comparison.json') 'Comparison manifest'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable
if ($manifest['schema'] -ne 1) { throw 'comparison.json must use schema 1.' }
if (-not ($manifest['expectedPackages'] -is [System.Collections.IDictionary]) -or $manifest['expectedPackages'].Count -eq 0) {
    throw 'comparison.json must contain a nonempty expectedPackages dictionary.'
}
if (-not $manifest.Contains('protectedFiles') -or @($manifest['protectedFiles']).Count -eq 0) {
    throw 'comparison.json must contain protectedFiles with original SHA256 hashes.'
}
if (-not $manifest.Contains('sourceFiles') -or @($manifest['sourceFiles']).Count -eq 0) {
    throw 'comparison.json must contain sourceFiles with snapshot SHA256 hashes.'
}
if (-not $manifest.Contains('inheritedConfigFiles') -or -not ($manifest['inheritedConfigFiles'] -is [System.Collections.IList]) -or $manifest['inheritedConfigFiles'].Count -eq 0) {
    throw 'comparison.json is missing inheritedConfigFiles. Re-prepare the comparison snapshot to freeze inherited build and analyzer configuration.'
}

$projectPath = Assert-VariantPath ([string]$manifest['projectPath']) 'Project path'
$projectDirectory = Assert-VariantPath (Join-Path $variantDirectory 'project') 'Snapshot project directory'
$projectPrefix = $projectDirectory + [IO.Path]::DirectorySeparatorChar
$outputPath = Assert-VariantPath ([string]$manifest['outputPath']) 'Output path'
$assetsPath = Assert-VariantPath (Join-Path $variantDirectory 'project/obj/project.assets.json') 'Assets path'
$reportName = if ($RequireOutput) { 'verification.json' } else { 'restore-verification.json' }
$reportPath = Assert-VariantPath (Join-Path $variantDirectory $reportName) 'Verification report'
$assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
if (-not ($assets['libraries'] -is [System.Collections.IDictionary])) {
    throw 'project.assets.json does not contain a libraries dictionary.'
}

$expected = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $manifest['expectedPackages'].GetEnumerator()) {
    if ([string]$entry.Key -notmatch '^[A-Za-z0-9_.-]+$' -or [string]::IsNullOrWhiteSpace([string]$entry.Value)) {
        throw 'expectedPackages contains an invalid package ID or version.'
    }
    $expected.Add([string]$entry.Key, [string]$entry.Value)
}
if (-not $expected.ContainsKey('Microsoft.WindowsAppSDK') -or $expected['Microsoft.WindowsAppSDK'] -ne [string]$manifest['sdkVersion']) {
    throw 'sdkVersion must match the expected Microsoft.WindowsAppSDK package version.'
}

$errors = [Collections.Generic.List[string]]::new()
$packageResults = [Collections.Generic.List[object]]::new()
$protectedResults = [Collections.Generic.List[object]]::new()
$inheritedConfigResults = [Collections.Generic.List[object]]::new()
$sourceResults = [Collections.Generic.List[object]]::new()
$applicationResults = [Collections.Generic.List[object]]::new()
$nativeResults = [Collections.Generic.List[object]]::new()
$libraries = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $assets['libraries'].GetEnumerator()) {
    $separator = $entry.Key.LastIndexOf('/')
    if ($separator -le 0) { continue }
    $id = $entry.Key.Substring(0, $separator)
    $version = $entry.Key.Substring($separator + 1)
    $isSdkPackage = $id -eq 'Microsoft.WindowsAppSDK' -or $id.StartsWith('Microsoft.WindowsAppSDK.', [StringComparison]::OrdinalIgnoreCase)
    if ($isSdkPackage -and -not $expected.ContainsKey($id)) {
        $errors.Add("Unexpected SDK package: $($entry.Key)")
    }
    if (-not $expected.ContainsKey($id)) { continue }
    $packageMatches = $version -eq $expected[$id] -and $entry.Value['type'] -eq 'package'
    $packageResults.Add([ordered]@{ id = $id; expectedVersion = $expected[$id]; actualVersion = $version; verified = $packageMatches })
    if (-not $packageMatches) { $errors.Add("Package does not match expected version/type: $($entry.Key)") }
    if ($libraries.ContainsKey($id)) { $errors.Add("Multiple restored versions of expected package: $id") }
    else { $libraries.Add($id, $entry.Value) }
}
foreach ($id in $expected.Keys) {
    if (-not $libraries.ContainsKey($id)) { $errors.Add("Expected package is missing: $id/$($expected[$id])") }
}

foreach ($configuration in $manifest['inheritedConfigFiles']) {
    $configurationSource = [string]$configuration['sourcePath']
    $configurationPath = [string]$configuration['path']
    $expectedHash = [string]$configuration['sha256']
    $sourceHash = $null
    $snapshotHash = $null
    try {
        if (-not [IO.Path]::IsPathFullyQualified($configurationSource) -or $expectedHash -notmatch '^[A-Fa-f0-9]{64}$') {
            throw 'Inherited configuration requires an absolute source path and a frozen SHA256 hash.'
        }
        $configurationSource = [IO.Path]::GetFullPath($configurationSource)
        $configurationPath = Assert-VariantPath $configurationPath 'Inherited configuration snapshot'
        $sourceHash = (Get-FileHash -LiteralPath $configurationSource -Algorithm SHA256).Hash
        $snapshotHash = (Get-FileHash -LiteralPath $configurationPath -Algorithm SHA256).Hash
        if ($sourceHash -ne $expectedHash) { $errors.Add("Inherited configuration source changed: $configurationSource") }
        if ($snapshotHash -ne $expectedHash) { $errors.Add("Inherited configuration snapshot changed: $configurationPath") }
    }
    catch { $errors.Add("Could not verify inherited configuration ${configurationPath}: $($_.Exception.Message)") }
    $inheritedConfigResults.Add([ordered]@{
        sourcePath = $configurationSource
        path = $configurationPath
        expectedSha256 = $expectedHash
        sourceSha256 = $sourceHash
        sha256 = $snapshotHash
        matchesSource = $null -ne $sourceHash -and $sourceHash -eq $snapshotHash
        verified = $null -ne $sourceHash -and $sourceHash -eq $expectedHash -and $snapshotHash -eq $expectedHash
    })
}

foreach ($snapshot in $manifest['sourceFiles']) {
    $relativePath = [string]$snapshot['relativePath']
    $expectedHash = [string]$snapshot['sha256']
    $snapshotPath = $null
    $actualHash = $null
    $status = 'error'
    $skipReason = $null
    try {
        if ([string]::IsNullOrWhiteSpace($relativePath) -or [IO.Path]::IsPathRooted($relativePath) -or $expectedHash -notmatch '^[A-Fa-f0-9]{64}$') {
            throw 'Source entries require a relative path and a SHA256 hash.'
        }
        $snapshotPath = Assert-VariantPath (Join-Path $projectDirectory $relativePath) 'Snapshot source file'
        if (-not $snapshotPath.StartsWith($projectPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Source files must remain inside VariantRoot/project.'
        }
        if (-not (Test-Path -LiteralPath $snapshotPath -PathType Leaf)) { throw 'Snapshot source file is missing.' }
        if ([IO.Path]::GetExtension($snapshotPath) -eq '.csproj') {
            $status = 'skipped'
            $skipReason = 'The comparison project is intentionally rewritten.'
        }
        else {
            $actualHash = (Get-FileHash -LiteralPath $snapshotPath -Algorithm SHA256).Hash
            $status = if ($actualHash -eq $expectedHash) { 'verified' } else { 'changed' }
            if ($status -eq 'changed') { $errors.Add("Snapshot source file changed: $snapshotPath") }
        }
    }
    catch { $errors.Add("Could not verify snapshot source ${relativePath}: $($_.Exception.Message)") }
    $sourceResults.Add([ordered]@{
        relativePath = $relativePath
        path = $snapshotPath
        expectedSha256 = $expectedHash
        sha256 = $actualHash
        status = $status
        skipReason = $skipReason
    })
}

foreach ($protected in $manifest['protectedFiles']) {
    $path = [string]$protected['path']
    $expectedHash = [string]$protected['sha256']
    $actualHash = $null
    if (-not [IO.Path]::IsPathFullyQualified($path) -or $expectedHash -notmatch '^[A-Fa-f0-9]{64}$') {
        $errors.Add("Invalid protected file entry: $path")
    }
    else {
        try { $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
        catch { $errors.Add("Could not read protected file: $path") }
        if ($actualHash -and $actualHash -ne $expectedHash) { $errors.Add("Protected file changed: $path") }
    }
    $protectedResults.Add([ordered]@{ path = $path; expectedSha256 = $expectedHash; sha256 = $actualHash; verified = $null -ne $actualHash -and $actualHash -eq $expectedHash })
}

if ($RequireOutput) {
    foreach ($applicationFile in @('PowerToys.PowerOCR.exe', 'PowerToys.PowerOCR.dll')) {
        $applicationPath = Assert-VariantPath (Join-Path $outputPath $applicationFile) 'PowerOCR application file'
        $applicationHash = $null
        $applicationVersion = $null
        try {
            $applicationHash = (Get-FileHash -LiteralPath $applicationPath -Algorithm SHA256).Hash
            $applicationVersion = (Get-Item -LiteralPath $applicationPath).VersionInfo.FileVersion
        }
        catch { $errors.Add("Could not hash application file: $applicationPath") }
        $applicationResults.Add([ordered]@{
            name = $applicationFile
            path = $applicationPath
            sha256 = $applicationHash
            fileVersion = $applicationVersion
            hashRecorded = $null -ne $applicationHash
        })
    }
    $nativePackages = [ordered]@{
        'Microsoft.InputStateManager.dll' = 'Microsoft.WindowsAppSDK.InteractiveExperiences'
        'Microsoft.UI.Input.dll' = 'Microsoft.WindowsAppSDK.InteractiveExperiences'
        'dwmcorei.dll' = 'Microsoft.WindowsAppSDK.InteractiveExperiences'
        'CoreMessagingXP.dll' = 'Microsoft.WindowsAppSDK.InteractiveExperiences'
        'dcompi.dll' = 'Microsoft.WindowsAppSDK.InteractiveExperiences'
        'Microsoft.ui.xaml.dll' = 'Microsoft.WindowsAppSDK.WinUI'
        'Microsoft.WindowsAppRuntime.dll' = 'Microsoft.WindowsAppSDK.Foundation'
    }
    if (-not ($assets['packageFolders'] -is [System.Collections.IDictionary])) {
        $errors.Add('project.assets.json does not contain packageFolders.')
    }
    foreach ($entry in $nativePackages.GetEnumerator()) {
        $destination = Assert-VariantPath (Join-Path $outputPath $entry.Key) 'Native output file'
        $source = $null
        $sourceHash = $null
        $outputHash = $null
        $fileVersion = $null
        try {
            if (-not $libraries.ContainsKey($entry.Value)) { throw 'The required SDK package is missing.' }
            $libraryPath = [string]$libraries[$entry.Value]['path']
            if ([string]::IsNullOrWhiteSpace($libraryPath) -or [IO.Path]::IsPathRooted($libraryPath)) { throw 'Invalid SDK library path.' }
            $expectedLibraryPath = $entry.Value + '/' + $expected[$entry.Value]
            if (-not $libraryPath.Replace('\', '/').Equals($expectedLibraryPath, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'SDK library path does not match its expected package ID/version.'
            }
            if (-not ($assets['packageFolders'] -is [System.Collections.IDictionary])) { throw 'Package folders are missing.' }
            foreach ($folder in $assets['packageFolders'].Keys) {
                if (-not [IO.Path]::IsPathFullyQualified($folder)) { throw 'Package folder must be absolute.' }
                $packageRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($folder)) + [IO.Path]::DirectorySeparatorChar
                $candidate = [IO.Path]::GetFullPath((Join-Path $packageRoot "$libraryPath/runtimes-framework/win-x64/native/$($entry.Key)"))
                if (-not $candidate.StartsWith($packageRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'SDK library path escapes its package folder.' }
                if (Test-Path -LiteralPath $candidate -PathType Leaf) { $source = $candidate; break }
            }
            if (-not $source) { throw 'The expected SDK native payload was not found in the package cache.' }
            $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
            $outputHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
            $fileVersion = (Get-Item -LiteralPath $destination).VersionInfo.FileVersion
            if ($sourceHash -ne $outputHash) { $errors.Add("Native output does not match its SDK package: $destination") }
        }
        catch { $errors.Add("Could not verify native file $($entry.Key): $($_.Exception.Message)") }
        $nativeResults.Add([ordered]@{
            name = $entry.Key
            package = $entry.Value
            sourcePath = $source
            outputPath = $destination
            fileVersion = $fileVersion
            expectedSha256 = $sourceHash
            sha256 = $outputHash
            verified = $null -ne $sourceHash -and $sourceHash -eq $outputHash
        })
    }
}

$report = [ordered]@{
    schema = 1
    checkedUtc = [DateTime]::UtcNow.ToString('O')
    sdkVersion = $manifest['sdkVersion']
    variantRoot = $variantDirectory
    projectPath = $projectPath
    outputPath = $outputPath
    assetsPath = $assetsPath
    requireOutput = [bool]$RequireOutput
    verified = $errors.Count -eq 0
    packages = $packageResults.ToArray()
    inheritedConfigFiles = $inheritedConfigResults.ToArray()
    sourceFiles = $sourceResults.ToArray()
    protectedFiles = $protectedResults.ToArray()
    applicationFiles = $applicationResults.ToArray()
    nativeFiles = $nativeResults.ToArray()
    errors = $errors.ToArray()
}
[IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
if ($errors.Count -ne 0) { throw "SDK comparison verification failed. Report: $reportPath" }
Write-Output $reportPath
