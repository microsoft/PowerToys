# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

function Get-InstallerMediaLayout {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [object[]]$Files,

        [ValidateRange(1, 2147483647)]
        [long]$MaximumUncompressedSize = 200MB
    )

    $sources = @{}
    $fileIds = @{}
    $entries = @(
        foreach ($file in ($Files | Sort-Object Id)) {
            if ($fileIds.ContainsKey($file.Id)) {
                throw "Duplicate installer File Id: $($file.Id)"
            }
            $fileIds[$file.Id] = $true

            $source = Get-Item -LiteralPath $file.SourcePath -ErrorAction Stop
            if (-not $sources.ContainsKey($source.FullName)) {
                $sources[$source.FullName] = @{
                    Size = $source.Length
                    Hash = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256 -ErrorAction Stop).Hash
                }
            }
            $content = $sources[$source.FullName]
            [pscustomobject]@{
                Id = $file.Id
                Component = $file.Component
                Size = [long]$content.Size
                Hash = $content.Hash
                DiskId = 0
            }
        }
    )

    # Disk 1 retains hand-authored files, including localized preprocessor loops.
    # Keep every content-equivalent group together: WiX only deduplicates within a CAB.
    $diskId = 2
    $cabinetSize = 0L
    $media = [Collections.Generic.List[object]]::new()
    foreach ($group in ($entries | Group-Object Hash | Sort-Object { $_.Group[0].Id })) {
        $groupSize = [long](($group.Group | Measure-Object Size -Sum).Sum)
        if ($cabinetSize -gt 0 -and $cabinetSize + $groupSize -gt $MaximumUncompressedSize) {
            $media.Add([pscustomobject]@{ Id = $diskId; Size = $cabinetSize })
            $diskId++
            $cabinetSize = 0L
        }
        foreach ($entry in $group.Group) {
            $entry.DiskId = $diskId
        }
        $cabinetSize += $groupSize
    }
    if ($entries.Count -gt 0) {
        $media.Add([pscustomobject]@{ Id = $diskId; Size = $cabinetSize })
    }

    [pscustomobject]@{
        Files = $entries
        Media = $media.ToArray()
        MaximumUncompressedSize = $MaximumUncompressedSize
    }
}

function Set-InstallerMedia {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [object[]]$Files,

        [Parameter(Mandatory)]
        [string]$ProductWxsPath,

        [Parameter(Mandatory)]
        [string]$ManifestPath,

        [ValidateRange(1, 2147483647)]
        [long]$MaximumUncompressedSize = 200MB
    )

    $layout = Get-InstallerMediaLayout -Files $Files -MaximumUncompressedSize $MaximumUncompressedSize
    $assignments = @{}
    foreach ($entry in $layout.Files) {
        $assignments[$entry.Id] = $entry
    }
    $encoding = New-Object Text.UTF8Encoding($false)
    foreach ($group in ($Files | Group-Object WxsPath)) {
        $document = New-Object Xml.XmlDocument
        $document.PreserveWhitespace = $true
        $document.Load($group.Name)
        $namespaces = New-Object Xml.XmlNamespaceManager($document.NameTable)
        $namespaces.AddNamespace('wix', 'http://wixtoolset.org/schemas/v4/wxs')
        $nodes = @{}
        foreach ($node in $document.SelectNodes('//wix:File', $namespaces)) {
            $nodes[$node.GetAttribute('Id')] = $node
        }
        foreach ($file in $group.Group) {
            if (-not $nodes.ContainsKey($file.Id)) {
                throw "Missing harvested File '$($file.Id)' in '$($group.Name)'"
            }
            $nodes[$file.Id].SetAttribute('DiskId', [string]$assignments[$file.Id].DiskId)
        }
        [IO.File]::WriteAllText($group.Name, $document.OuterXml, $encoding)
    }

    $product = Get-Content -LiteralPath $ProductWxsPath -Raw
    $mediaPattern = '(?s)<!--InstallerMedia-->.*?<!--/InstallerMedia-->'
    if ([regex]::Matches($product, $mediaPattern).Count -ne 1) {
        throw "Expected one bounded InstallerMedia section in '$ProductWxsPath'"
    }
    $definitions = @(
        foreach ($cabinet in $layout.Media) {
            '    <Media Id="{0}" Cabinet="cab{0}.cab" EmbedCab="yes" CompressionLevel="high" />' -f $cabinet.Id
        }
    ) -join "`r`n"
    $section = "<!--InstallerMedia-->`r`n$definitions`r`n    <!--/InstallerMedia-->"
    [IO.File]::WriteAllText($ProductWxsPath, [regex]::Replace($product, $mediaPattern, $section), $encoding)

    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $ManifestPath) -Force
    [IO.File]::WriteAllText($ManifestPath, ($layout | ConvertTo-Json -Depth 5), $encoding)
    Write-Host "Assigned $($layout.Files.Count) MSI files to $($layout.Media.Count) Smart Cabbing cabinets; retained all File rows."
}
