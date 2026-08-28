[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True)]
    [ValidateNotNullOrEmpty()]
    [string[]]$MsiPath,
    [Parameter(Mandatory = $True)]
    [ValidateNotNullOrEmpty()]
    [string]$BuildOutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$fileHashByPath = @{}

function Get-CachedFileHash {
    Param(
        [Parameter(Mandatory = $True)]
        [string]$Path
    )

    if (-not $fileHashByPath.ContainsKey($Path)) {
        $fileHashByPath[$Path] = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    }

    return $fileHashByPath[$Path]
}

function Get-LongMsiFileName {
    Param(
        [Parameter(Mandatory = $True)]
        [string]$FileName
    )

    $names = $FileName -split '\|', 2
    return $names[$names.Count - 1]
}

function Invoke-MsiQuery {
    Param(
        [Parameter(Mandatory = $True)]
        [object]$Database,
        [Parameter(Mandatory = $True)]
        [string]$Query,
        [Parameter(Mandatory = $True)]
        [string[]]$ColumnNames,
        [Parameter(Mandatory = $True)]
        [ValidateSet('String', 'Integer')]
        [string[]]$ColumnTypes
    )

    if ($ColumnNames.Count -ne $ColumnTypes.Count) {
        throw 'ColumnNames and ColumnTypes must have the same number of entries.'
    }

    $view = $Database.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $Database, @($Query))
    try {
        $null = $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null)
        $rows = New-Object System.Collections.Generic.List[object]

        while ($record = $view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)) {
            try {
                $row = [ordered]@{}
                for ($index = 0; $index -lt $ColumnNames.Count; $index++) {
                    $property = if ($ColumnTypes[$index] -eq 'Integer') { 'IntegerData' } else { 'StringData' }
                    $row[$ColumnNames[$index]] = $record.GetType().InvokeMember($property, 'GetProperty', $null, $record, $index + 1)
                }
                $rows.Add([pscustomobject]$row)
            } finally {
                [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
            }
        }

        return $rows.ToArray()
    } finally {
        $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null) | Out-Null
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
    }
}

function Assert-ActionBefore {
    Param(
        [Parameter(Mandatory = $True)]
        [hashtable]$SequenceByAction,
        [Parameter(Mandatory = $True)]
        [string]$First,
        [Parameter(Mandatory = $True)]
        [string]$Second,
        [Parameter(Mandatory = $True)]
        [string]$PackagePath
    )

    if (-not $SequenceByAction.ContainsKey($First) -or -not $SequenceByAction.ContainsKey($Second)) {
        throw "Missing $First or $Second in InstallExecuteSequence for $PackagePath."
    }

    if ($SequenceByAction[$First] -ge $SequenceByAction[$Second]) {
        throw "$First must run before $Second in $PackagePath."
    }
}

function Get-WinUI3DuplicateFileData {
    Param(
        [Parameter(Mandatory = $True)]
        [string]$PackagePath,
        [Parameter(Mandatory = $True)]
        [string]$OutputPath
    )

    $resolvedPath = (Resolve-Path -LiteralPath $PackagePath).Path
    $resolvedOutputPath = (Resolve-Path -LiteralPath $OutputPath).Path
    $winUI3OutputPath = Join-Path $resolvedOutputPath 'WinUI3Apps'
    if (-not (Test-Path -LiteralPath $winUI3OutputPath -PathType Container)) {
        throw "WinUI3Apps build output is missing from $resolvedOutputPath."
    }

    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $installer, @($resolvedPath, 0))

    try {
        $tables = Invoke-MsiQuery $database 'SELECT `Name` FROM `_Tables`' @('Name') @('String')
        if ($tables.Name -notcontains 'DuplicateFile') {
            throw "DuplicateFile table is missing from $resolvedPath."
        }

        $duplicateRows = Invoke-MsiQuery $database 'SELECT `FileKey`, `Component_`, `File_`, `DestName`, `DestFolder` FROM `DuplicateFile`' @('FileKey', 'Component', 'File', 'DestinationName', 'DestinationFolder') @('String', 'String', 'String', 'String', 'String')
        $winUI3Duplicates = @($duplicateRows | Where-Object { $_.FileKey -like 'WinUI3AppsDuplicate_*' })
        if ($winUI3Duplicates.Count -eq 0) {
            throw "No WinUI3Apps DuplicateFile entries were found in $resolvedPath."
        }

        if (@($winUI3Duplicates | Where-Object { $_.DestinationFolder -ne 'WinUI3AppsInstallFolder' }).Count -ne 0) {
            throw "Unexpected WinUI3Apps DuplicateFile destination found in $resolvedPath."
        }

        $fileRows = Invoke-MsiQuery $database 'SELECT `File`, `Component_`, `FileName` FROM `File`' @('File', 'Component', 'FileName') @('String', 'String', 'String')
        $filesById = @{}
        foreach ($fileRow in $fileRows) {
            $filesById[$fileRow.File] = $fileRow
        }

        $componentRows = Invoke-MsiQuery $database 'SELECT `Component`, `Directory_` FROM `Component`' @('Component', 'Directory') @('String', 'String')
        $directoriesByComponent = @{}
        foreach ($componentRow in $componentRows) {
            $directoriesByComponent[$componentRow.Component] = $componentRow.Directory
        }

        foreach ($duplicate in $winUI3Duplicates) {
            if (-not [string]::IsNullOrEmpty($duplicate.DestinationName)) {
                throw "DuplicateFile $($duplicate.FileKey) changes the destination name in $resolvedPath."
            }
            if (-not $filesById.ContainsKey($duplicate.File)) {
                throw "DuplicateFile $($duplicate.FileKey) references missing File row $($duplicate.File) in $resolvedPath."
            }
            if ($filesById[$duplicate.File].Component -ne $duplicate.Component) {
                throw "DuplicateFile $($duplicate.FileKey) is not owned by its source component in $resolvedPath."
            }
            if (-not $directoriesByComponent.ContainsKey($duplicate.Component) -or $directoriesByComponent[$duplicate.Component] -ne 'INSTALLFOLDER') {
                throw "DuplicateFile $($duplicate.FileKey) does not copy from INSTALLFOLDER in $resolvedPath."
            }
        }

        $regularWinUI3FileNames = @{}
        foreach ($fileRow in $fileRows) {
            if ($directoriesByComponent.ContainsKey($fileRow.Component) -and $directoriesByComponent[$fileRow.Component] -eq 'WinUI3AppsInstallFolder') {
                $regularWinUI3FileNames[(Get-LongMsiFileName $fileRow.FileName)] = $True
            }
        }

        foreach ($duplicate in $winUI3Duplicates) {
            $longFileName = Get-LongMsiFileName ($filesById[$duplicate.File].FileName)
            if ($regularWinUI3FileNames.ContainsKey($longFileName)) {
                throw "$longFileName is authored as both a File and a DuplicateFile in WinUI3Apps in $resolvedPath."
            }
        }

        $duplicatesBySourceFile = @{}
        foreach ($duplicate in $winUI3Duplicates) {
            if ($duplicatesBySourceFile.ContainsKey($duplicate.File)) {
                throw "Multiple WinUI3Apps DuplicateFile rows reference $($duplicate.File) in $resolvedPath."
            }
            $duplicatesBySourceFile[$duplicate.File] = $duplicate
        }

        $expectedSourceFiles = New-Object System.Collections.Generic.List[string]
        foreach ($fileRow in $fileRows | Where-Object { $_.Component -eq 'BaseApplicationsFiles_Component' }) {
            $longFileName = Get-LongMsiFileName $fileRow.FileName
            $rootFile = Join-Path $resolvedOutputPath $longFileName
            $winUI3File = Join-Path $winUI3OutputPath $longFileName

            if ((Test-Path -LiteralPath $rootFile -PathType Leaf) -and (Test-Path -LiteralPath $winUI3File -PathType Leaf)) {
                if ((Get-CachedFileHash $rootFile) -eq (Get-CachedFileHash $winUI3File)) {
                    $expectedSourceFiles.Add($fileRow.File)
                }
            }
        }

        # A zero-size set would mean the installer-size optimization disappeared or the build
        # layout changed. Fail loudly so the packaging logic is reviewed instead of silently
        # dropping the DuplicateFile coverage.
        if ($expectedSourceFiles.Count -eq 0) {
            throw "No identical root/WinUI3Apps build outputs were found for $resolvedPath."
        }

        foreach ($sourceFile in $expectedSourceFiles) {
            if (-not $duplicatesBySourceFile.ContainsKey($sourceFile)) {
                throw "Missing WinUI3Apps DuplicateFile row for $sourceFile in $resolvedPath."
            }
        }

        foreach ($duplicate in $winUI3Duplicates) {
            $sourceFile = $filesById[$duplicate.File]
            $longFileName = Get-LongMsiFileName $sourceFile.FileName
            $rootFile = Join-Path $resolvedOutputPath $longFileName
            $winUI3File = Join-Path $winUI3OutputPath $longFileName

            if (-not (Test-Path -LiteralPath $rootFile -PathType Leaf) -or -not (Test-Path -LiteralPath $winUI3File -PathType Leaf)) {
                throw "Build output is missing for DuplicateFile $($duplicate.FileKey) in $resolvedPath."
            }
            if ((Get-CachedFileHash $rootFile) -ne (Get-CachedFileHash $winUI3File)) {
                throw "DuplicateFile $($duplicate.FileKey) references non-identical build outputs in $resolvedPath."
            }
        }

        if ($winUI3Duplicates.Count -ne $expectedSourceFiles.Count) {
            throw "Expected $($expectedSourceFiles.Count) WinUI3Apps DuplicateFile rows but found $($winUI3Duplicates.Count) in $resolvedPath."
        }

        $customActions = Invoke-MsiQuery $database 'SELECT `Action` FROM `CustomAction`' @('Action') @('String')
        $legacyActions = @('SetCreateWinAppSDKHardlinksParam', 'CreateWinAppSDKHardlinks', 'SetDeleteWinAppSDKHardlinksParam', 'DeleteWinAppSDKHardlinks')
        foreach ($legacyAction in $legacyActions) {
            if ($customActions.Action -contains $legacyAction) {
                throw "Legacy custom action $legacyAction is still present in $resolvedPath."
            }
        }

        if ($fileRows.File -contains 'WinUI3Apps_hardlinks_txt' -or $fileRows.FileName -match '(^|\|)hardlinks\.txt$') {
            throw "Legacy hardlinks.txt manifest is still present in $resolvedPath."
        }

        $sequenceRows = Invoke-MsiQuery $database 'SELECT `Action`, `Sequence` FROM `InstallExecuteSequence`' @('Action', 'Sequence') @('String', 'Integer')
        $sequenceByAction = @{}
        foreach ($sequenceRow in $sequenceRows) {
            $sequenceByAction[$sequenceRow.Action] = $sequenceRow.Sequence
        }

        Assert-ActionBefore $sequenceByAction 'DuplicateFiles' 'InstallCmdPalPackage' $resolvedPath
        Assert-ActionBefore $sequenceByAction 'InstallFiles' 'DuplicateFiles' $resolvedPath
        Assert-ActionBefore $sequenceByAction 'InstallCmdPalPackage' 'InstallPackageIdentityMSIX' $resolvedPath
        Assert-ActionBefore $sequenceByAction 'InstallValidate' 'RemoveDuplicateFiles' $resolvedPath
        Assert-ActionBefore $sequenceByAction 'UnRegisterContextMenuPackages' 'RemoveDuplicateFiles' $resolvedPath
        Assert-ActionBefore $sequenceByAction 'UninstallPackageIdentityMSIX' 'RemoveDuplicateFiles' $resolvedPath
        Assert-ActionBefore $sequenceByAction 'RemoveDuplicateFiles' 'RemoveFiles' $resolvedPath
        Assert-ActionBefore $sequenceByAction 'RemoveDuplicateFiles' 'InstallFiles' $resolvedPath
        if ($sequenceByAction.ContainsKey('PatchFiles')) {
            Assert-ActionBefore $sequenceByAction 'PatchFiles' 'DuplicateFiles' $resolvedPath
        }

        Write-Host "Verified $($winUI3Duplicates.Count) WinUI3Apps DuplicateFile entries in $resolvedPath"
        return [pscustomobject]@{
            Path = $resolvedPath
            DuplicateKeys = @($winUI3Duplicates | ForEach-Object { "$($_.File)|$($_.DestinationFolder)|$($_.DestinationName)" } | Sort-Object)
        }
    } finally {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)
    }
}

$packages = @($MsiPath | ForEach-Object { Get-WinUI3DuplicateFileData $_ $BuildOutputPath })
$referenceKeys = $packages[0].DuplicateKeys

foreach ($package in $packages | Select-Object -Skip 1) {
    $difference = Compare-Object $referenceKeys $package.DuplicateKeys
    if ($null -ne $difference) {
        throw "WinUI3Apps DuplicateFile entries differ between $($packages[0].Path) and $($package.Path)."
    }
}

Write-Host 'WinUI3Apps DuplicateFile verification completed successfully.'
