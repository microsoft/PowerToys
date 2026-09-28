# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$MsiPath,
    [Parameter(Mandatory)][ValidateSet('true', 'false')][string]$PerUser,
    [Parameter(Mandatory)][string]$ExpectedLifecyclePath,
    [Parameter(Mandatory)][string]$ExtractedBinaryPath
)
$ErrorActionPreference = 'Stop'
$msi = $database = $null

function Read-MsiRows([string]$Query, [string[]]$Columns, [int]$StreamSizeField = 0) {
    $view = $null
    try {
        $view = $database.OpenView($Query)
        $view.Execute()
        while ($null -ne ($record = $view.Fetch())) {
            try {
                $values = [ordered]@{}
                for ($index = 1; $index -le $Columns.Count; ++$index) {
                    $values[$Columns[$index - 1]] = if ($index -eq $StreamSizeField) {
                        $record.DataSize($index)
                    } else {
                        $record.StringData($index)
                    }
                }
                [pscustomobject]$values
            } finally {
                [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
            }
        }
        $view.Close()
    } finally {
        if ($null -ne $view) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) }
    }
}

try {
    $path = (Resolve-Path -LiteralPath $MsiPath).Path
    $msi = New-Object -ComObject WindowsInstaller.Installer
    $database = $msi.OpenDatabase($path, 0)
    $tables = @(Read-MsiRows 'SELECT `Name` FROM `_Tables`' @('Name'))
    $binaries = @()
    $actions = @()
    $sequence = @()
    if ('Binary' -in $tables.Name) {
        $binaries = @(Read-MsiRows 'SELECT `Name`, `Data` FROM `Binary`' @('Name', 'Size') 2 |
            Where-Object Name -CEQ 'PTStorageRemoval')
    }
    if ('CustomAction' -in $tables.Name) {
        $actions = @(Read-MsiRows 'SELECT `Action`, `Type`, `Source`, `Target` FROM `CustomAction`' @('Name', 'Type', 'Source', 'Target') |
            Where-Object Name -CEQ 'RemoveProtectedStorageInstances')
    }
    if ('InstallExecuteSequence' -in $tables.Name) {
        $sequence = @(Read-MsiRows 'SELECT `Action`, `Condition`, `Sequence` FROM `InstallExecuteSequence`' @('Name', 'Condition', 'Sequence'))
    }
    $scheduled = @($sequence | Where-Object Name -CEQ 'RemoveProtectedStorageInstances')
    if ($PerUser -eq 'true') {
        if ($binaries.Count -or $actions.Count -or $scheduled.Count -or (Test-Path -LiteralPath $ExtractedBinaryPath)) {
            throw 'Per-user main MSI must not contain the SYSTEM all-owner cleanup payload, action, or schedule.'
        }
    } else {
        if ($binaries.Count -ne 1 -or $actions.Count -ne 1 -or $scheduled.Count -ne 1) {
            throw 'Per-machine main MSI must link exactly one cleanup Binary, CustomAction, and sequence entry; source authoring alone is not sufficient.'
        }
        # EXE in Binary + continue-on-error + commit + in-script + no impersonation.
        $expectedType = 2 -bor 0x40 -bor 0x200 -bor 0x400 -bor 0x800
        if ([int]$actions[0].Type -ne $expectedType -or $actions[0].Source -cne 'PTStorageRemoval' -or
            $actions[0].Target -cne 'machine-remove --keep-data') {
            throw 'Compiled cleanup must use the embedded Lifecycle with fixed KeepData, commit, SYSTEM, and best-effort semantics.'
        }
        $condition = [regex]::Replace($scheduled[0].Condition, '\s+', ' ').Trim()
        if ($condition -cne 'Installed AND REMOVE~="ALL" AND NOT UPGRADINGPRODUCTCODE') {
            throw 'Compiled cleanup must be restricted to final full uninstall, excluding major-upgrade removal.'
        }
        $removeFiles = @($sequence | Where-Object Name -CEQ 'RemoveFiles')
        $finalize = @($sequence | Where-Object Name -CEQ 'InstallFinalize')
        if ($removeFiles.Count -ne 1 -or $finalize.Count -ne 1 -or
            [int]$scheduled[0].Sequence -le [int]$removeFiles[0].Sequence -or
            [int]$scheduled[0].Sequence -ge [int]$finalize[0].Sequence) {
            throw 'Compiled commit cleanup must be scheduled after RemoveFiles and before InstallFinalize.'
        }
        $expected = Get-Item -LiteralPath $ExpectedLifecyclePath
        if ($binaries[0].Size -ne $expected.Length -or !(Test-Path -LiteralPath $ExtractedBinaryPath -PathType Leaf) -or
            (Get-FileHash -LiteralPath $ExtractedBinaryPath -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $ExpectedLifecyclePath -Algorithm SHA256).Hash) {
            throw 'The compiled cleanup Binary must contain the exact verified release Lifecycle bytes.'
        }
    }
    Write-Host "PASS compiled protected-storage MSI tables and payload: PerUser=$PerUser; $path"
} finally {
    foreach ($object in @($database, $msi)) {
        if ($null -ne $object) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($object) }
    }
}
