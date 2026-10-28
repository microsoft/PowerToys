# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

function Get-PowerToysReleaseTitle {
    <#
    .SYNOPSIS
        Selects the release title and pads three-component display versions without changing tags.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Tag,
        [ValidateSet("refs/heads/main", "refs/heads/stable")][string]$SourceBranch,
        [switch]$Prerelease
    )

    $displayTag = if ($Tag -match '^v[0-9]+\.[0-9]+\.[0-9]+$') { "$Tag.0" } else { $Tag }

    if (-not $Prerelease) {
        return "Release $displayTag"
    }

    switch ($SourceBranch) {
        "refs/heads/stable" { return "Release preview $displayTag" }
        "refs/heads/main" { return "Development preview $displayTag" }
        default { throw "A prerelease title requires the build's SourceBranch (refs/heads/main or refs/heads/stable)." }
    }
}
