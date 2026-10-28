# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Get-UmdfHidUiTestSelection {
    param(
        [string[]]$Modules = @(),
        [string]$Platform,
        [bool]$AutoSelect = $false,
        [bool]$RouteArm64 = $false,
        [bool]$DedicatedArm64 = $false
    )
    if ($DedicatedArm64 -and ($Platform -ne 'ARM64' -or $AutoSelect -or
        $Modules.Count -ne 1 -or $Modules[0] -ne 'GrabAndMove.UITests')) {
        throw 'The dedicated ARM64 UMDF job must select exactly GrabAndMove.UITests without PR auto-selection.'
    }

    $explicit = 'GrabAndMove.UITests' -in $Modules
    $selected = $Modules.Count -eq 0 -or $explicit
    $supported = $Platform -in @('x64', 'x64Win10', 'x64Win11') -or $DedicatedArm64
    $routed = $selected -and $Platform -eq 'ARM64' -and $RouteArm64 -and -not $DedicatedArm64
    $blocked = $selected -and -not $supported -and $explicit -and -not $AutoSelect -and -not $routed
    [pscustomobject]@{
        Run = $selected -and $supported
        Exclude = $selected -and -not $supported
        Blocked = $blocked
        Skipped = $selected -and -not $supported -and -not $blocked -and -not $routed
        Routed = $routed
    }
}

function Assert-UmdfHidArm64TestEnvironment {
    param([string]$OsArchitecture, [string]$BootConfiguration)
    if ($OsArchitecture -ne 'ARM64') {
        throw 'BLOCKED_INFRASTRUCTURE: the dedicated UMDF test image must run on ARM64.'
    }
    if ($BootConfiguration -notmatch '(?im)^\s*testsigning\s+Yes\s*$') {
        throw 'BLOCKED_INFRASTRUCTURE: the dedicated ARM64 image must already have TESTSIGNING enabled and be rebooted. The pipeline does not change boot policy.'
    }
}
