function Test-PreviewReleaseAssetBuildMarker {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$MarkerPath,
        [Parameter(Mandatory)][int]$BuildId,
        [Parameter(Mandatory)][string]$Version
    )

    if (-not (Test-Path -LiteralPath $MarkerPath -PathType Leaf)) {
        return $false
    }

    $marker = Get-Content -LiteralPath $MarkerPath -Raw | ConvertFrom-Json
    return [int]$marker.buildId -eq $BuildId -and [string]$marker.version -eq $Version
}

function Assert-PreviewReleaseZipReadable {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        if ($archive.Entries.Count -eq 0) {
            throw "ZIP archive '$Path' is empty."
        }

        $buffer = [byte[]]::new(81920)
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) {
                continue
            }

            $stream = $entry.Open()
            try {
                while ($stream.Read($buffer, 0, $buffer.Length) -gt 0) {
                }
            }
            finally {
                $stream.Dispose()
            }
        }

        return @($archive.Entries | ForEach-Object { $_.FullName.Replace("\", "/") })
    }
    finally {
        $archive.Dispose()
    }
}

function Get-PreviewReleaseAssets {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$AssetsDirectory
    )

    $manifestPath = Join-Path $AssetsDirectory "assets-manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Release asset manifest not found: $manifestPath"
    }

    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Release asset manifest '$manifestPath' is invalid JSON. $_"
    }

    if ([int]$manifest.schemaVersion -ne 1) {
        throw "Release asset manifest '$manifestPath' has unsupported schema version '$($manifest.schemaVersion)'."
    }

    $items = @($manifest.assets)
    if ($items.Count -eq 0) {
        throw "Release asset manifest '$manifestPath' does not declare any assets."
    }

    $declaredNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $files = @()
    foreach ($item in $items) {
        $name = [string]$item.name
        if ([string]::IsNullOrWhiteSpace($name) -or
            [System.IO.Path]::GetFileName($name) -ne $name -or
            [System.IO.Path]::GetExtension($name) -notin @(".exe", ".zip")) {
            throw "Release asset manifest contains invalid asset name '$name'."
        }
        if (-not $declaredNames.Add($name)) {
            throw "Release asset manifest contains duplicate asset name '$name'."
        }

        $path = Join-Path $AssetsDirectory $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Manifest-declared release asset not found: $path"
        }

        $file = Get-Item -LiteralPath $path
        if ([long]$item.size -ne [long]$file.Length) {
            throw "Release asset '$name' size '$($file.Length)' does not match manifest size '$($item.size)'."
        }

        $expectedHash = [string]$item.sha256
        if ($expectedHash -notmatch "^[0-9a-fA-F]{64}$") {
            throw "Release asset '$name' has an invalid SHA256 value in assets-manifest.json."
        }
        $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($actualHash -ne $expectedHash) {
            throw "Release asset '$name' SHA256 '$actualHash' does not match manifest SHA256 '$expectedHash'."
        }

        $files += $file
    }

    $extraCandidates = @(
        Get-ChildItem -LiteralPath $AssetsDirectory -File |
            Where-Object {
                $_.Extension -in @(".exe", ".zip") -and
                -not $declaredNames.Contains($_.Name)
            }
    )
    if ($extraCandidates.Count -gt 0) {
        throw "Assets directory contains undeclared release files: $(($extraCandidates.Name | Sort-Object) -join ', ')"
    }

    return @($files | Sort-Object FullName -Unique)
}
