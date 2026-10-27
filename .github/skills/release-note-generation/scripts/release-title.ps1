# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

function Get-PowerToysReleaseTitle {
    <#
    .SYNOPSIS
        Selects the release title from the requested release type and build source branch.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Tag,
        [ValidateSet("refs/heads/main", "refs/heads/stable")][string]$SourceBranch,
        [switch]$Prerelease
    )

    if (-not $Prerelease) {
        return "Release $Tag"
    }

    switch ($SourceBranch) {
        "refs/heads/stable" { return "Release preview $Tag" }
        "refs/heads/main" { return "Development preview $Tag" }
        default { throw "A prerelease title requires the build's SourceBranch (refs/heads/main or refs/heads/stable)." }
    }
}
