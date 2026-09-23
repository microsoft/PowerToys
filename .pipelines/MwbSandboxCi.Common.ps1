# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Get-MwbCiBackend {
    param([string] $Platform, [int] $WindowsBuild)

    if ($Platform -ceq 'x64Win10' -and $WindowsBuild -ge 19041 -and $WindowsBuild -lt 22000) {
        return 'Legacy'
    }
    # ARM64 has no Windows 10 tier in this pilot: the single arm64 test pool is a Windows 11
    # 24H2+ image, so it only ever selects the modern WinApp backend, exactly like x64Win11.
    if (($Platform -ceq 'x64Win11' -or $Platform -ceq 'arm64') -and $WindowsBuild -ge 26100) {
        return 'WinApp'
    }
    throw "BLOCKED_INFRASTRUCTURE: MWB platform $Platform does not match Windows build $WindowsBuild. Win11 (x64 or ARM64) requires the registered modern Sandbox client on 24H2 or newer; no Legacy fallback is allowed."
}

function Get-MwbCiArchitecture {
    param([string] $Platform)

    if ($Platform -ceq 'x64Win10' -or $Platform -ceq 'x64Win11') { return 'x64' }
    if ($Platform -ceq 'arm64') { return 'arm64' }
    throw "BLOCKED_INFRASTRUCTURE: unrecognized MWB test platform $Platform."
}

function Get-MwbPreviewPin {
    Get-Content -LiteralPath (Join-Path $PSScriptRoot 'mwb-sandbox-preview\source.json') -Raw | ConvertFrom-Json
}

function Get-MwbPreviewPatchPath {
    Join-Path $PSScriptRoot "mwb-sandbox-preview\$((Get-MwbPreviewPin).Patch)"
}

function Resolve-MwbCiFileSystemPath {
    param([string] $Path, [switch] $AllowMissing)

    $provider = $null
    $drive = $null
    $full = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path, [ref]$provider, [ref]$drive)
    if ($provider.Name -ne 'FileSystem') { throw 'MWB CI preparation paths must use the FileSystem provider.' }
    $ancestor = [IO.Path]::GetFullPath($full)
    $missing = [Collections.Generic.Stack[string]]::new()
    if ($AllowMissing) {
        while (-not (Test-Path -LiteralPath $ancestor)) {
            $parent = [IO.Path]::GetDirectoryName($ancestor)
            if ([string]::IsNullOrEmpty($parent) -or $parent -eq $ancestor) {
                throw 'The MWB CI preparation path has no existing filesystem ancestor.'
            }
            $missing.Push([IO.Path]::GetFileName($ancestor))
            $ancestor = $parent
        }
    }
    # Match Resolve-MwbArchiveFileSystemPath: Resolve-Path can preserve DOS aliases.
    # Canonicalize the existing filesystem ancestor before adding missing descendants.
    $item = Get-Item -LiteralPath $ancestor -Force -ErrorAction Stop
    if ($missing.Count -and -not $item.PSIsContainer) {
        throw 'The MWB CI preparation path has a non-directory ancestor.'
    }
    $normalized = $item.FullName
    while ($missing.Count) { $normalized = [IO.Path]::Combine($normalized, $missing.Pop()) }
    [IO.Path]::GetFullPath($normalized)
}

function Assert-MwbCiPlainPath {
    param([string] $Path)

    if ($Path -notmatch '^[A-Za-z]:\\') { throw 'MWB CI requires local absolute artifact paths.' }
    $ancestor = [IO.Path]::GetFullPath($Path)
    while ($ancestor) {
        if ((Test-Path -LiteralPath $ancestor) -and
            ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'MWB CI artifact paths must not contain reparse points.'
        }
        $ancestor = Split-Path $ancestor -Parent
    }
}

function Assert-MwbCiHash {
    param([string] $Path, [string] $Sha256)

    Assert-MwbCiPlainPath $Path
    if ($Sha256 -notmatch '^[0-9a-fA-F]{64}$' -or
        -not (Test-Path -LiteralPath $Path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ine $Sha256) {
        throw "MWB CI artifact hash mismatch: $([IO.Path]::GetFileName($Path))."
    }
}

function Get-MwbCiRelativePath {
    param([string] $Root, [string] $RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        $RelativePath -match '(^[\\/]|[:"<>|?*\x00-\x1f]|(^|[\\/])\.\.?([\\/]|$)|[. ]([\\/]|$))') {
        throw 'MWB CI manifests require confined relative file paths.'
    }
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $path = [IO.Path]::GetFullPath((Join-Path $rootPath $RelativePath))
    if (-not $path.StartsWith("$rootPath\", [StringComparison]::OrdinalIgnoreCase)) {
        throw 'MWB CI manifest file escaped its root.'
    }
    $path
}

function Get-MwbCiBundle {
    param([string] $Root, [string] $SourceRevision, [ValidateSet('x64', 'arm64')][string] $Platform = 'x64')

    $bundle = Join-Path $Root 'mwb-sandbox-ci'
    $manifestPath = Join-Path $bundle 'manifest.json'
    Assert-MwbCiPlainPath $manifestPath
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "BLOCKED_INFRASTRUCTURE: build-$Platform-Debug lacks the build-job MWB CI bundle. Rebuild the gated pilot; test agents never build or download preview/compiler bits."
    }
    $manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
    $manifest = [Text.Encoding]::UTF8.GetString($manifestBytes).TrimStart([char]0xFEFF) | ConvertFrom-Json
    $manifestHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($manifestBytes))
    $pin = Get-MwbPreviewPin
    if ($SourceRevision -notmatch '^[0-9a-fA-F]{40}$' -or
        $manifest.FormatVersion -ne 1 -or $manifest.Platform -cne $Platform -or
        $manifest.Configuration -cne 'Debug' -or $manifest.SourceRevision -ine $SourceRevision -or
        $manifest.Preview.Repository -cne $pin.Repository -or
        $manifest.Preview.Commit -cne $pin.Commit -or
        $manifest.Preview.PatchSha256 -ine $pin.PatchSha256 -or
        $manifest.Preview.Version -cne $pin.Version -or
        $manifest.Preview.SdkVersion -cne $pin.SdkVersion -or
        $manifest.Preview.RuntimeVersion -cne $pin.RuntimeVersion) {
        throw 'MWB CI bundle provenance does not match the selected Debug revision and pinned preview source.'
    }
    Assert-MwbCiHash (Join-Path $bundle 'runtime.zip') $manifest.Runtime.Sha256
    Assert-MwbCiHash (Join-Path $bundle 'runtime.zip.manifest.json') $manifest.Runtime.ManifestSha256
    $runtime = Get-Content -LiteralPath (Join-Path $bundle 'runtime.zip.manifest.json') -Raw | ConvertFrom-Json
    if ($runtime.Sha256 -ine $manifest.Runtime.Sha256 -or $runtime.FileCount -ne @($manifest.Runtime.Files).Count -or
        $manifest.Runtime.ReadyToRun -isnot [bool] -or
        ($manifest.Runtime.ReadyToRun -and -not $runtime.PSObject.Properties['ReadyToRun'])) {
        throw 'MWB CI runtime archive provenance is inconsistent.'
    }
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $manifest.Runtime.Files) {
        $null = Get-MwbCiRelativePath $bundle $entry.Path
        if ($entry.Sha256 -notmatch '^[0-9a-fA-F]{64}$' -or -not $names.Add($entry.Path) -or
            $entry.Path -notin $runtime.Files) {
            throw 'MWB CI runtime file inventory is inconsistent.'
        }
    }
    foreach ($required in @('PowerToys.exe', 'PowerToys.MouseWithoutBorders.exe',
        'PowerToys.MouseWithoutBorders.dll', 'PowerToys.MouseWithoutBordersHelper.exe',
        'WinUI3Apps\PowerToys.Settings.exe', 'WinUI3Apps\PowerToys.QuickAccess.exe')) {
        if (-not $names.Contains($required)) { throw "MWB CI runtime closure is missing $required." }
    }
    if ($manifest.Runtime.ReadyToRun) {
        $compilation = $runtime.ReadyToRun
        if ($compilation.FormatVersion -ne 1 -or $compilation.ManagedILAndAttributesUnchanged -cne $true -or
            $compilation.CompiledAssemblies -lt 1 -or $compilation.CompiledAssemblies -ne @($compilation.Assemblies).Count -or
            @($compilation.Compiler.Files).Count -ne 3) {
            throw 'MWB CI ReadyToRun verification provenance is incomplete.'
        }
        foreach ($assembly in $compilation.Assemblies) {
            foreach ($path in $assembly.Paths) {
                $entries = @($manifest.Runtime.Files | Where-Object Path -CEQ $path)
                if ($entries.Count -ne 1 -or $entries[0].Sha256 -ine $assembly.OutputSha256) {
                    throw 'MWB CI ReadyToRun output hashes disagree with the runtime inventory.'
                }
            }
        }
    }
    if (@($manifest.Preview.Files).Count -ne @($pin.Files).Count) {
        throw 'MWB CI preview portable file inventory is inconsistent.'
    }
    $previewNames = @($manifest.Preview.Files | ForEach-Object Path)
    foreach ($name in $pin.Files) {
        $entries = @($manifest.Preview.Files | Where-Object Path -CEQ $name)
        if ($entries.Count -ne 1) { throw 'MWB CI preview portable file inventory is inconsistent.' }
        Assert-MwbCiHash (Join-Path $bundle "preview\$name") $entries[0].Sha256
    }
    $actualPreview = @(Get-ChildItem -LiteralPath (Join-Path $bundle 'preview') -File -Recurse -Force)
    if ($actualPreview.Count -ne $previewNames.Count) {
        throw 'MWB CI preview contains unmanifested files.'
    }
    [pscustomobject]@{
        Root = $bundle
        Manifest = $manifest
        ManifestSha256 = $manifestHash
    }
}

function Expand-MwbCiRuntime {
    param([string] $Archive, [string] $Destination, [object[]] $Files)

    Assert-MwbCiPlainPath $Archive
    Assert-MwbCiPlainPath $Destination
    if (Test-Path -LiteralPath $Destination) { throw 'MWB CI host staging must be a new private directory.' }
    $expected = @{}
    foreach ($file in $Files) {
        $path = Get-MwbCiRelativePath $Destination $file.Path
        if ($expected.ContainsKey($path)) { throw 'MWB CI runtime has duplicate paths.' }
        $expected[$path] = $file.Sha256
    }
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        $entries = @{}
        foreach ($entry in $zip.Entries) {
            $relative = $entry.FullName -replace '^\.\/', ''
            if (-not $relative -or $relative.EndsWith('/')) { continue }
            $path = Get-MwbCiRelativePath $Destination $relative
            if (-not $expected.ContainsKey($path) -or $entries.ContainsKey($path) -or
                (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) {
                throw 'MWB CI archive contains an unexpected, duplicate, or symbolic-link file.'
            }
            $entries[$path] = $entry
        }
        if ($entries.Count -ne $expected.Count) { throw 'MWB CI archive file inventory is incomplete.' }
        foreach ($path in $entries.Keys) {
            $null = New-Item -ItemType Directory -Path (Split-Path $path) -Force
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entries[$path], $path, $false)
            Assert-MwbCiHash $path $expected[$path]
        }
    }
    finally { $zip.Dispose() }
}
