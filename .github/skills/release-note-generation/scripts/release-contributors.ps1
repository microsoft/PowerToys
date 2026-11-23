# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

function Get-ReleaseContributorLogin {
    param([Parameter(Mandatory)]$Contributor)

    if ($Contributor -is [string]) {
        return $Contributor.Trim()
    }
    return [string]$Contributor.login
}

function Test-ReleaseContributorBot {
    param([Parameter(Mandatory)]$Contributor)

    $login = Get-ReleaseContributorLogin -Contributor $Contributor
    if ($login -match '(^app/|\[bot\]$|^(copilot|github-copilot|copilot-swe-agent|dependabot|github-actions)$)') {
        return $true
    }
    foreach ($property in @("is_bot", "type", "__typename")) {
        $value = $Contributor.PSObject.Properties[$property]
        if ($value -and (($property -eq "is_bot" -and $value.Value -eq $true) -or
                ($property -ne "is_bot" -and $value.Value -eq "Bot"))) {
            return $true
        }
    }
    return $false
}

function Get-ReleaseExternalContributors {
    <#
    .SYNOPSIS
        Selects human contributors outside the refreshed PowerToys member list.
    #>
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Contributors,
        [Parameter(Mandatory)][string[]]$Members
    )

    $memberSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($member in $Members) {
        [void]$memberSet.Add($member)
    }
    $logins = foreach ($contributor in $Contributors) {
        if ($null -eq $contributor) {
            continue
        }
        $login = Get-ReleaseContributorLogin -Contributor $contributor
        if ($login -and -not $memberSet.Contains($login) -and -not (Test-ReleaseContributorBot -Contributor $contributor)) {
            $login
        }
    }
    return $logins | Sort-Object -Unique
}

function Get-ReleaseCommitAuthors {
    <#
    .SYNOPSIS
        Reads commit authors and coauthors resolved by GitHub, including all author pages.
    #>
    param(
        [Parameter(Mandatory)][string[]]$CommitShas,
        [string]$Repo = "microsoft/PowerToys"
    )

    $owner, $name = $Repo -split "/", 2
    $query = 'query($owner:String!,$name:String!,$sha:String!,$cursor:String) { repository(owner:$owner,name:$name) { object(expression:$sha) { ... on Commit { authors(first:100,after:$cursor) { nodes { user { login } } pageInfo { hasNextPage endCursor } } } } } }'
    foreach ($sha in $CommitShas) {
        $cursor = $null
        do {
            $arguments = @("api", "graphql", "-f", "query=$query", "-f", "owner=$owner", "-f", "name=$name", "-f", "sha=$sha")
            if ($cursor) {
                $arguments += @("-f", "cursor=$cursor")
            }
            $json = & gh @arguments
            if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($json | Out-String))) {
                throw "Failed to fetch release contributor attribution for commit '$sha'."
            }
            $result = $json | ConvertFrom-Json
            $authors = $result.data.repository.object.authors
            if (-not $authors) {
                throw "GitHub did not return contributor attribution for commit '$sha'."
            }
            foreach ($node in $authors.nodes) {
                if ($node.user -and $node.user.login) {
                    $node.user
                }
            }
            $cursor = $authors.pageInfo.endCursor
        } while ($authors.pageInfo.hasNextPage)
    }
}
