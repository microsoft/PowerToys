# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Get-UmdfHidUiTestSelection {
    param(
        [string[]]$Modules = @(),
        [string]$Platform,
        [bool]$AutoSelect = $false
    )
    $explicit = 'GrabAndMove.UITests' -in $Modules
    $selected = $Modules.Count -eq 0 -or $explicit
    $supported = $Platform -in @('x64', 'x64Win10', 'x64Win11')
    $blocked = $selected -and -not $supported -and $explicit -and -not $AutoSelect
    [pscustomobject]@{
        Run = $selected -and $supported
        Exclude = $selected -and -not $supported
        Blocked = $blocked
        Skipped = $selected -and -not $supported -and -not $blocked
    }
}
