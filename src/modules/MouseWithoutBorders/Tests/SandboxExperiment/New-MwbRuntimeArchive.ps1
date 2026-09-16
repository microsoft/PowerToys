# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Packages the MWB, helper, Settings and Quick Access dependency closure for Sandbox.
.DESCRIPTION
Run during payload preparation, not in the interactive MSTest process. Managed
assets come from the built .deps.json files. Native MWB activation libraries and
WinUI resources are included explicitly. No build, installation, or UI is run.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProductRoot,
    [Parameter(Mandatory = $true)][string]$ArchivePath
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProductRoot).Path.TrimEnd('\')
$archive = [IO.Path]::GetFullPath($ArchivePath)
$stage = "$archive.staging"
if (Test-Path -LiteralPath $archive) { throw 'Use a new archive path to preserve payload provenance.' }
if (Test-Path -LiteralPath $stage) { throw 'The staging directory already exists.' }
$files = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
if (-not ('MwbPeImports' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
public static class MwbPeImports
{
    public static string[] Read(string path)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var stream = File.OpenRead(path))
        using (var image = new PEReader(stream))
        {
            var header = image.PEHeaders.PEHeader;
            if (header == null) { return Array.Empty<string>(); }
            ReadTable(image, header.ImportTableDirectory, false, header.ImageBase, names);
            ReadTable(image, header.DelayImportTableDirectory, true, header.ImageBase, names);
        }
        return new List<string>(names).ToArray();
    }
    private static void ReadTable(PEReader image, DirectoryEntry entry, bool delay, ulong imageBase, HashSet<string> names)
    {
        if (entry.RelativeVirtualAddress == 0 || entry.Size == 0) { return; }
        var reader = image.GetSectionData(entry.RelativeVirtualAddress).GetReader();
        int length = Math.Min(reader.Length, entry.Size);
        int size = delay ? 32 : 20;
        while (reader.Offset + size <= length)
        {
            uint first = reader.ReadUInt32();
            uint second = reader.ReadUInt32();
            uint third = reader.ReadUInt32();
            uint fourth = reader.ReadUInt32();
            uint fifth = reader.ReadUInt32();
            uint name = delay ? second : fourth;
            if (delay) { reader.ReadUInt32(); reader.ReadUInt32(); reader.ReadUInt32(); }
            if ((first | second | third | fourth | fifth) == 0) { break; }
            if (name == 0) { continue; }
            ulong rva = name;
            if (delay && (first & 1) == 0)
            {
                if (rva < imageBase) { throw new BadImageFormatException("Invalid delay-import address."); }
                rva -= imageBase;
            }
            if (rva > int.MaxValue) { throw new BadImageFormatException("Import address is outside the image."); }
            var text = image.GetSectionData((int)rva).GetReader();
            var value = new StringBuilder();
            for (int i = 0; i < 260 && text.RemainingBytes > 0; i++)
            {
                byte character = text.ReadByte();
                if (character == 0) { break; }
                value.Append((char)character);
            }
            string dll = value.ToString();
            if (dll.Length == 0 || dll.IndexOfAny(new[] { '\\', '/', ':' }) >= 0)
            {
                throw new BadImageFormatException("Invalid imported library name.");
            }
            names.Add(dll);
        }
    }
}
'@
}

function Add-RuntimeFile {
    param([string]$Path)
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith("$root\", [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $full -PathType Leaf)) {
        throw "A required runtime file is missing or outside the payload: $full"
    }
    $null = $files.Add($full.Substring($root.Length + 1))
}

foreach ($application in @(
    'PowerToys.MouseWithoutBorders',
    'PowerToys.MouseWithoutBordersHelper',
    'WinUI3Apps\PowerToys.Settings',
    'WinUI3Apps\PowerToys.QuickAccess'
)) {
    $base = Join-Path $root $application
    foreach ($extension in @('.exe', '.dll', '.deps.json', '.runtimeconfig.json')) {
        Add-RuntimeFile "$base$extension"
    }
    $directory = Split-Path $base
    $deps = [IO.File]::ReadAllText("$base.deps.json") | ConvertFrom-Json
    $target = $deps.targets.PSObject.Properties[$deps.runtimeTarget.name].Value
    foreach ($library in $target.PSObject.Properties) {
        foreach ($kind in @('runtime', 'native', 'resources', 'runtimeTargets')) {
            $group = $library.Value.PSObject.Properties[$kind]
            if (-not $group) { continue }
            foreach ($asset in $group.Value.PSObject.Properties) {
                if ($kind -eq 'runtimeTargets' -and $asset.Value.rid -notmatch '^win(?:10)?-x64$') { continue }
                $name = [IO.Path]::GetFileName($asset.Name)
                if ($name -eq '_._') { continue }
                $candidates = @((Join-Path $directory $asset.Name), (Join-Path $directory $name))
                if ($kind -eq 'resources' -and $asset.Value.locale) {
                    $candidates = @((Join-Path $directory "$($asset.Value.locale)\$name")) + $candidates
                }
                $found = @($candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
                if (-not $found.Count) { throw "Missing $application dependency: $($asset.Name)" }
                Add-RuntimeFile $found[0]
            }
        }
    }
}
foreach ($name in @('PowerToys.exe', 'PowerToys.MouseWithoutBordersModuleInterface.dll',
    'PowerToys.GPOWrapper.dll', 'PowerToys.Interop.dll')) {
    Add-RuntimeFile (Join-Path $root $name)
}
foreach ($name in @('PowerToys.GPOWrapper.dll', 'PowerToys.Interop.dll', 'PowerToys.ZoomItSettingsInterop.dll')) {
    Add-RuntimeFile (Join-Path $root "WinUI3Apps\$name")
}
# These Windows App SDK components are dynamically loaded, not discoverable
# from PE imports/.deps.json. WinUIEdit serves Microsoft.UI.Text.FontWeights,
# which the toolkit's FontIconExtension activates while parsing Settings XAML.
foreach ($name in @('dcompi.dll', 'dwmcorei.dll', 'DwmSceneI.dll', 'WinUIEdit.dll',
    'Microsoft.DirectManipulation.dll', 'Microsoft.Graphics.Display.dll',
    'PushNotificationsLongRunningTask.ProxyStub.dll')) {
    Add-RuntimeFile (Join-Path $root "WinUI3Apps\$name")
}
# XAML class libraries deploy resources beside the assembly in a same-name
# directory. The assembly dependency itself does not include those loose files.
foreach ($relative in @($files | Where-Object { [IO.Path]::GetExtension($_) -eq '.dll' })) {
    $resourceRoot = Join-Path (Split-Path (Join-Path $root $relative)) ([IO.Path]::GetFileNameWithoutExtension($relative))
    if (Test-Path -LiteralPath $resourceRoot -PathType Container) {
        Get-ChildItem -LiteralPath $resourceRoot -File -Recurse | ForEach-Object { Add-RuntimeFile $_.FullName }
    }
}
# The shared WinUI PRI also refers to loose XAML/XBF outside assembly-named
# folders (for example Quick Access's /Resources/Styles dictionaries).
Get-ChildItem -LiteralPath (Join-Path $root 'WinUI3Apps') -File -Recurse |
    Where-Object { $_.Extension -in '.xaml', '.xbf' } |
    ForEach-Object { Add-RuntimeFile $_.FullName }
# Pri/XAML and WinUI native runtime files are not all represented as managed dependencies.
foreach ($directory in @($root, (Join-Path $root 'WinUI3Apps'))) {
    Get-ChildItem -LiteralPath $directory -File | Where-Object {
        $_.Extension -in '.pri', '.winmd' -or $_.Name -match '^(Microsoft\.(UI|Windows)|WindowsAppRuntime|WebView2Loader|DWriteCore|Microsoft\.Interop)'
    } | ForEach-Object {
        if ($_.Extension -notin '.pdb', '.lib', '.exp', '.ilk', '.idb') { Add-RuntimeFile $_.FullName }
    }
    foreach ($folder in @('Assets', 'SettingsXAML', 'QuickAccessXaml', 'Controls', 'Microsoft.UI.Xaml', 'amd64')) {
        $path = Join-Path $directory $folder
        if (Test-Path -LiteralPath $path -PathType Container) {
            Get-ChildItem -LiteralPath $path -File -Recurse | ForEach-Object { Add-RuntimeFile $_.FullName }
        }
    }
    # WinUI's native implementation has its own import graph (CoreMessagingXP,
    # DwmSceneI, MRM, debug CRT, etc.); .deps.json alone does not describe it.
    $visited = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    do {
        $pending = @($files | Where-Object {
            [IO.Path]::GetExtension($_) -in '.dll', '.exe' -and -not $visited.Contains($_)
        })
        foreach ($relative in $pending) {
            $null = $visited.Add($relative)
            $path = Join-Path $root $relative
            foreach ($dependency in [MwbPeImports]::Read($path)) {
                $candidates = @((Join-Path (Split-Path $path) $dependency), (Join-Path $root $dependency))
                $found = @($candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
                if ($found.Count) { Add-RuntimeFile $found[0] }
            }
        }
    } while ($pending.Count)
    Get-ChildItem -LiteralPath $directory -Directory | Where-Object { $_.Name -match '^[a-z]{2,3}(?:-[A-Za-z0-9]+)*$' } |
        ForEach-Object {
            Get-ChildItem -LiteralPath $_.FullName -File -Recurse |
                Where-Object { $_.Extension -in '.pri', '.mui' } |
                ForEach-Object { Add-RuntimeFile $_.FullName }
        }
}
$null = New-Item -ItemType Directory -Path $stage -Force
foreach ($relative in $files) {
    $destination = Join-Path $stage $relative
    $null = New-Item -ItemType Directory -Path (Split-Path $destination) -Force
    Copy-Item -LiteralPath (Join-Path $root $relative) -Destination $destination
}
& tar.exe -a -cf $archive -C $stage .
if ($LASTEXITCODE -ne 0) { throw 'Native runtime archive packaging failed.' }
[ordered]@{
    ProductRoot = $root; ArchivePath = $archive; FileCount = $files.Count
    Length = (Get-Item -LiteralPath $archive).Length
    Sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    Files = @($files | Sort-Object)
} | ConvertTo-Json -Depth 4 | Set-Content "$archive.manifest.json"
[pscustomobject]@{ ArchivePath = $archive; FileCount = $files.Count; Megabytes = [Math]::Round((Get-Item $archive).Length / 1MB, 1) }
