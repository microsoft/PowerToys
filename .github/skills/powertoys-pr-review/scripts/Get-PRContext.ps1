<#
.SYNOPSIS
    Fetch the context needed to calibrate Phase 0 for a PowerToys PR.
.DESCRIPTION
    The list endpoint does not expose author association for pull requests, so
    this reads the single-PR endpoint and returns author login, author
    association (drives the community-vs-member bar), draft state, and size.
.PARAMETER PRNumber
    The microsoft/PowerToys PR number.
.PARAMETER Repo
    Target repo. Default microsoft/PowerToys.
.EXAMPLE
    ./Get-PRContext.ps1 -PRNumber 43741
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [int] $PRNumber,
    [string] $Repo = 'microsoft/PowerToys'
)

$ErrorActionPreference = 'Stop'

$ctx = gh api "repos/$Repo/pulls/$PRNumber" `
    --jq '{author: .user.login, assoc: .author_association, draft: .draft, additions, deletions, changedFiles: .changed_files, title: .title, labels: [.labels[].name]}' |
    ConvertFrom-Json

$isCommunity = $ctx.assoc -in @('CONTRIBUTOR', 'FIRST_TIME_CONTRIBUTOR', 'NONE')
$ctx | Add-Member -NotePropertyName 'IsCommunity' -NotePropertyValue $isCommunity -PassThru |
    Format-List | Out-String | Write-Host

return $ctx
