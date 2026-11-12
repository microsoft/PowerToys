# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. (Join-Path $PSScriptRoot '..\umdf\UmdfCi.Common.ps1')

# Retain the original entry points for standalone smoke pipelines and existing callers.
function Get-UmdfHidUiTestSelection {
    param(
        [string[]]$Modules = @(), [string]$Platform, [bool]$AutoSelect = $false,
        [bool]$RouteArm64 = $false, [bool]$DedicatedArm64 = $false,
        [ValidateSet('GrabAndMove.UITests', 'PowerDisplay.UITests')][string]$FixtureModule = 'GrabAndMove.UITests'
    )
    Get-UmdfUiTestSelection @PSBoundParameters
}

function Assert-UmdfHidArm64TestEnvironment {
    param([string]$OsArchitecture, [string]$BootConfiguration)
    Assert-UmdfArm64TestEnvironment @PSBoundParameters
}
