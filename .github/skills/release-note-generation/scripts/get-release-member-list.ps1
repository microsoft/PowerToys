<#
.SYNOPSIS
    Refreshes release-attribution exclusions from the live PowerToys code-owner team and core roster.
.DESCRIPTION
    Uses authenticated GitHub CLI team membership rather than organization-level MEMBER badges.
    Stops on inaccessible or empty team data; never silently falls back to the Markdown roster alone.
    TeamMembersJsonPath accepts an explicit saved GitHub team-members response for offline review.
.EXAMPLE
    .\get-release-member-list.ps1 -OutputPath '.\Generated Files\ReleaseNotes\MemberList.md'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$CommunityPath = (Join-Path $PSScriptRoot "..\..\..\..\COMMUNITY.md"),
    [string]$TeamMembersJsonPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$community = Get-Content -LiteralPath $CommunityPath -Raw
$coreTeam = [regex]::Match($community, '(?ms)^## PowerToys core team\r?\n(?<body>.*?)(?=^## |\z)')
$coreLogins = @([regex]::Matches($coreTeam.Groups["body"].Value, '\[@([a-zA-Z0-9-]+)\]') |
    ForEach-Object { $_.Groups[1].Value })
if ($coreLogins.Count -eq 0) {
    throw "The PowerToys core-team roster is missing or empty in '$CommunityPath'."
}

if ($TeamMembersJsonPath) {
    $teamJson = Get-Content -LiteralPath $TeamMembersJsonPath -Raw
}
else {
    $teamJson = gh api --paginate --slurp "orgs/microsoft/teams/powertoys-code-owners/members?per_page=100"
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot refresh PowerToys code-owner membership. Authenticate gh with team-read access or supply an explicitly reviewed -TeamMembersJsonPath snapshot."
    }
}
$teamMembers = @($teamJson | ConvertFrom-Json | ForEach-Object { $_ } | ForEach-Object { $_ })
$teamLogins = @($teamMembers | ForEach-Object { [string]$_.login })
if ($teamLogins.Count -eq 0 -or @($teamLogins | Where-Object { $_ -notmatch '^[a-zA-Z0-9-]+$' }).Count -gt 0) {
    throw "PowerToys code-owner membership is empty or invalid; attribution exclusions were not updated."
}

$members = @(@($coreLogins) + @($teamLogins) | Sort-Object -Unique)
$parent = Split-Path -Parent $OutputPath
if ($parent) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
$members | Set-Content -LiteralPath $OutputPath -Encoding utf8
[pscustomobject]@{
    memberCount = $members.Count
    teamMemberCount = $teamLogins.Count
    outputPath = (Resolve-Path -LiteralPath $OutputPath).Path
}
