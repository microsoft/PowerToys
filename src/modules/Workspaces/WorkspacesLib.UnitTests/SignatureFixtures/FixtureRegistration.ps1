# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Update-FixtureShortcuts {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][object[]]$Fixtures,
        [Parameter(Mandatory)][string]$OutputDirectory,
        [Parameter(Mandatory)][string]$ShortcutFolder,
        [Parameter(Mandatory)][string]$Owner,
        [Parameter(Mandatory)][object]$Shell,
        [switch]$Remove
    )

    if (Test-Path -LiteralPath $ShortcutFolder) {
        $folder = Get-Item -LiteralPath $ShortcutFolder -Force
        if (!$folder.PSIsContainer -or ($folder.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "The fixture shortcut folder is not an ordinary directory: $ShortcutFolder"
        }
    }
    # Preflight every existing shortcut before changing any of them.
    $plans = foreach ($fixture in $Fixtures) {
        $target = Join-Path $OutputDirectory $fixture.file
        $linkPath = Join-Path $ShortcutFolder ([IO.Path]::GetFileNameWithoutExtension($fixture.file) + '.lnk')
        if (Test-Path -LiteralPath $linkPath) {
            $item = Get-Item -LiteralPath $linkPath -Force
            if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw "Refusing to modify a directory or reparse point: $linkPath"
            }
            $existing = $Shell.CreateShortcut($linkPath)
            if ($existing.TargetPath -ne $target -or $existing.Description -cne $Owner -or
                $existing.Arguments -ne '' -or $existing.WorkingDirectory -ne $OutputDirectory) {
                throw "An unrelated or modified shortcut occupies $linkPath; refusing to modify any shortcuts."
            }
        }
        [pscustomobject]@{ shortcut = $linkPath; target = $target }
    }

    $changedAny = $false
    foreach ($plan in $plans) {
        $changed = $false
        $action = if ($Remove) { 'Remove generated fixture shortcut' } else { 'Register generated fixture shortcut' }
        if ((!$Remove -or (Test-Path -LiteralPath $plan.shortcut)) -and
            $PSCmdlet.ShouldProcess($plan.shortcut, $action)) {
            if ($Remove) {
                Remove-Item -LiteralPath $plan.shortcut
            }
            else {
                if (!(Test-Path -LiteralPath $ShortcutFolder)) {
                    New-Item -ItemType Directory -Path $ShortcutFolder | Out-Null
                }
                $shortcut = $Shell.CreateShortcut($plan.shortcut)
                $shortcut.TargetPath = $plan.target
                $shortcut.Arguments = ''
                $shortcut.WorkingDirectory = $OutputDirectory
                $shortcut.Description = $Owner
                $shortcut.WindowStyle = 1
                $shortcut.Save()
            }
            $changed = $true
            $changedAny = $true
        }
        [pscustomobject]@{ shortcut = $plan.shortcut; target = $plan.target; action = $action; changed = $changed }
    }
    if ($Remove -and $changedAny -and (Test-Path -LiteralPath $ShortcutFolder) -and
        @(Get-ChildItem -LiteralPath $ShortcutFolder -Force).Count -eq 0 -and
        $PSCmdlet.ShouldProcess($ShortcutFolder, 'Remove empty fixture shortcut folder')) {
        [IO.Directory]::Delete($ShortcutFolder, $false)
    }
}

function Get-FixtureAppsFolderMetadata {
    param(
        [Parameter(Mandatory)][object]$AppsFolder,
        [Parameter(Mandatory)][string[]]$Targets
    )

    foreach ($item in $AppsFolder.Items()) {
        $path = $item.ExtendedProperty('System.Link.TargetParsingPath')
        if ($path -in $Targets) {
            $hostEnvironment = $item.ExtendedProperty('System.AppUserModel.HostEnvironment')
            [pscustomobject]@{
                path = $path
                hostEnvironment = $hostEnvironment
                # Missing Shell metadata must not be converted to integer zero.
                canLaunchElevated = $null -ne $hostEnvironment -and [string]$hostEnvironment -eq '0'
            }
        }
    }
}
