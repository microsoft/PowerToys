<#
.SYNOPSIS
Converts C++ projects (.vcxproj) from packages.config to PackageReference with central package versions.

.DESCRIPTION
For every .vcxproj under -Path that has a sibling packages.config, this script:
  - adds the native PackageReference properties and one versionless PackageReference per package
    (versions come from Directory.Packages.props),
  - removes the version-pinned <Import> lines into the packages folder,
  - removes the EnsureNuGetPackageBuildImports target and the packages.config item (.vcxproj and .vcxproj.filters),
  - removes '<...>packages\<Id>.<Version>\include' include directories for referenced packages
    (the package's own targets already add them),
  - deletes packages.config.

Packages imported by the project, or whose include folder it hard-codes, are added even when
missing from packages.config. The script fails if a package is not listed in Directory.Packages.props,
and reports any remaining hard-coded 'packages\' paths for manual follow-up.

Projects are skipped (with a warning) when a .csproj consumes them through a ProjectReference
without ReferenceOutputAssembly=false, because NuGet then fails restore with NU1201, or when
their folder holds another project that would share obj\project.assets.json.

.PARAMETER Path
One or more folders (searched recursively) or .vcxproj files to convert.

.EXAMPLE
.\tools\build\convert-packages-config.ps1 -Path src\common

.EXAMPLE
.\tools\build\convert-packages-config.ps1 -Path src\modules\fancyzones -WhatIf
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param (
    [Parameter(Mandatory = $true)]
    [string[]]$Path
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
[xml]$centralProps = Get-Content (Join-Path $repoRoot 'Directory.Packages.props') -Raw
$centralVersions = @{}
foreach ($pv in $centralProps.Project.ItemGroup.PackageVersion) {
    if ($pv.Include) { $centralVersions[$pv.Include] = $pv.Version }
}

$packageFolderPattern = 'packages\\(?<folder>[A-Za-z][^\\"<>;]*\.\d+(?:\.\d+)+(?:-[^\\"<>;]+)?)\\'

# Package folders are '<Id>.<Version>'; prefer the ids from packages.config since ids can end in digits.
function Resolve-PackageId([string]$folder, $configPackages) {
    foreach ($package in $configPackages) {
        if ($folder -eq "$($package.id).$($package.version)") { return $package.id }
    }

    return [regex]::Match($folder, '^(?<id>.+?)\.\d+\.\d+\.\d+(?:\.\d+)?(?:-.+)?$').Groups['id'].Value
}

$nugetBlock = @'
  <PropertyGroup Label="NuGet">
    <!-- Tell NuGet this is PackageReference style -->
    <RestoreProjectStyle>PackageReference</RestoreProjectStyle>
    <!-- Tell NuGet we're a native project -->
    <NuGetTargetMoniker>native,Version=v0.0</NuGetTargetMoniker>
    <!-- Tell NuGet we target Windows (use your existing WindowsTargetPlatformVersion) -->
    <NuGetTargetPlatformIdentifier>Windows</NuGetTargetPlatformIdentifier>
    <NuGetTargetPlatformVersion>$(WindowsTargetPlatformVersion)</NuGetTargetPlatformVersion>
  </PropertyGroup>
'@

function Read-TextFile([string]$file) {
    $bytes = [System.IO.File]::ReadAllBytes($file)
    $hasByteOrderMark = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($hasByteOrderMark) { $text = $text.Substring(1) }
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    return [pscustomobject]@{ Text = $text; HasByteOrderMark = $hasByteOrderMark; Newline = $newline }
}

function Write-TextFile([string]$file, [string]$text, [bool]$hasByteOrderMark) {
    [System.IO.File]::WriteAllText($file, $text, (New-Object System.Text.UTF8Encoding($hasByteOrderMark)))
}

function Remove-PackagesConfigItem([string]$text) {
    $item = '<None Include="packages\.config"\s*(?:/>|>(?:(?!</None>).)*</None>)'
    $text = [regex]::Replace($text, '(?ms)^[ \t]*<ItemGroup>\s*' + $item + '\s*</ItemGroup>[ \t]*\r?\n', '')
    return [regex]::Replace($text, '(?ms)^[ \t]*' + $item + '[ \t]*\r?\n', '')
}

# NuGet fails restore (NU1201) when a managed project references a native PackageReference project
# and consumes its output, so those projects need C#-side changes before they can move.
$managedReferences = @{}
foreach ($csproj in Get-ChildItem -Path (Join-Path $repoRoot 'src'), (Join-Path $repoRoot 'tools') -Recurse -Filter *.csproj) {
    [xml]$csprojXml = Get-Content $csproj.FullName -Raw
    foreach ($reference in $csprojXml.SelectNodes('//*[local-name()="ProjectReference"]')) {
        $include = $reference.GetAttribute('Include')
        if ($include -notlike '*.vcxproj') { continue }

        $referenceOutputAssembly = $reference.GetAttribute('ReferenceOutputAssembly')
        $metadata = $reference.SelectSingleNode('*[local-name()="ReferenceOutputAssembly"]')
        if ($metadata) { $referenceOutputAssembly = $metadata.InnerText }
        if ($referenceOutputAssembly -eq 'false') { continue }

        $include = $include.Replace('$(RepoRoot)', "$repoRoot\").Replace('$(SolutionDir)', "$repoRoot\")
        $target = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($csproj.DirectoryName, $include))
        $managedReferences[$target] = @($managedReferences[$target]) + $csproj.Name | Where-Object { $_ }
    }
}

$projects = foreach ($p in $Path) {
    $resolved = (Resolve-Path $p).Path
    if ((Get-Item $resolved).PSIsContainer) {
        Get-ChildItem -Path $resolved -Recurse -Filter *.vcxproj
    } else {
        Get-Item $resolved
    }
}

$converted = 0
$warnings = @()
foreach ($project in ($projects | Sort-Object FullName -Unique)) {
    $packagesConfig = Join-Path $project.DirectoryName 'packages.config'
    if (-not (Test-Path $packagesConfig)) { continue }

    $relative = $project.FullName.Substring($repoRoot.Length + 1)
    $siblings = @(Get-ChildItem -Path $project.DirectoryName -File | Where-Object { $_.Extension -in '.vcxproj', '.csproj', '.wixproj' })
    if ($siblings.Count -gt 1) {
        $warnings += "${relative}: skipped, folder has several projects that would share obj\project.assets.json."
        continue
    }

    if ($managedReferences.ContainsKey($project.FullName)) {
        $warnings += "${relative}: skipped, referenced by $(($managedReferences[$project.FullName] | Sort-Object -Unique) -join ', ') (NU1201)."
        continue
    }

    $file = Read-TextFile $project.FullName
    $text = $file.Text
    $nl = $file.Newline

    # Packages from packages.config plus any package the project imports or includes directly.
    $packageIds = [System.Collections.Generic.List[string]]::new()
    [xml]$config = Get-Content $packagesConfig -Raw
    $configPackages = @($config.packages.package)
    foreach ($package in $configPackages) {
        if (-not $packageIds.Contains($package.id)) { $packageIds.Add($package.id) }
        if ($centralVersions.ContainsKey($package.id) -and $centralVersions[$package.id] -ne $package.version) {
            $warnings += "${relative}: $($package.id) moves from $($package.version) to central $($centralVersions[$package.id])."
        }
    }

    $usedPackages = @([regex]::Matches($text, '<Import\s+Project="[^"]*' + $packageFolderPattern)) + @([regex]::Matches($text, $packageFolderPattern + 'include\\?;'))
    foreach ($match in $usedPackages) {
        $id = Resolve-PackageId $match.Groups['folder'].Value $configPackages
        if ($id -and -not $packageIds.Contains($id)) {
            $packageIds.Add($id)
            $warnings += "${relative}: $id is used but missing from packages.config; added."
        }
    }

    $missing = @($packageIds | Where-Object { -not $centralVersions.ContainsKey($_) })
    if ($missing.Count -gt 0) {
        $warnings += "${relative}: skipped, add PackageVersion entries to Directory.Packages.props first: $($missing -join ', ')."
        continue
    }

    # Drop version-pinned imports, the EnsureNuGetPackageBuildImports target and the packages.config item.
    $text = [regex]::Replace($text, '(?ms)^[ \t]*<Import\s+Project="[^"]*' + $packageFolderPattern + '[^"]*"[^>]*/>[ \t]*\r?\n', '')
    $text = [regex]::Replace($text, '(?ms)^[ \t]*<Target Name="EnsureNuGetPackageBuildImports".*?</Target>[ \t]*\r?\n', '')
    $text = Remove-PackagesConfigItem $text

    # Package targets add their own include folders, so hard-coded ones are no longer needed.
    $text = [regex]::Replace($text, '[^;<>"]*' + $packageFolderPattern + 'include\\?;', '')

    $references = ($packageIds | Sort-Object | ForEach-Object { "    <PackageReference Include=""$_"" GeneratePathProperty=""true"" />" }) -join $nl
    $insert = ($nugetBlock -replace "`r?`n", $nl) + $nl + "  <ItemGroup>$nl$references$nl  </ItemGroup>$nl"
    $globals = [regex]::Match($text, '(?ms)^[ \t]*<PropertyGroup Label="Globals">.*?</PropertyGroup>[ \t]*\r?\n')
    if (-not $globals.Success) {
        throw "${relative}: no <PropertyGroup Label=""Globals""> found to anchor the PackageReference items."
    }

    $text = $text.Insert($globals.Index + $globals.Length, $insert)

    foreach ($leftover in [regex]::Matches($text, '[^;<>"]*' + $packageFolderPattern + '[^;<>"]*')) {
        $warnings += "${relative}: hard-coded package path remains: $($leftover.Value.Trim())"
    }

    if ($PSCmdlet.ShouldProcess($relative, "Convert $($packageIds -join ', ') to PackageReference")) {
        Write-TextFile $project.FullName $text $file.HasByteOrderMark

        $filters = "$($project.FullName).filters"
        if (Test-Path $filters) {
            $filtersFile = Read-TextFile $filters
            $filtersText = Remove-PackagesConfigItem $filtersFile.Text
            if ($filtersText -ne $filtersFile.Text) {
                Write-TextFile $filters $filtersText $filtersFile.HasByteOrderMark
            }
        }

        Remove-Item $packagesConfig
        Write-Host "Converted $relative ($($packageIds -join ', '))"
        $converted++
    }
}

foreach ($warning in $warnings) {
    Write-Warning $warning
}

Write-Host "Converted $converted project(s)."
