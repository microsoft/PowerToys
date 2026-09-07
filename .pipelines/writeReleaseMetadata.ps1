[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$DefinitionId,
    [Parameter(Mandatory)][int]$BuildId,
    [Parameter(Mandatory)][string]$BuildNumber,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][ValidateSet("private", "preview", "stable")][string]$Channel,
    [Parameter(Mandatory)][ValidateSet("private-validation", "preview-validation", "preview-release", "stable-release")][string]$Intent,
    [Parameter(Mandatory)][string]$SourceBranch,
    [Parameter(Mandatory)][string]$SourceCommit,
    [Parameter(Mandatory)][string]$BuildReason,
    [Parameter(Mandatory)][string]$ShouldPublishPreview,
    [AllowEmptyString()][string]$QueuedAt = "",
    [AllowEmptyString()][string]$StartedAt = "",
    [Parameter(Mandatory)][string]$OutputPath
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch "^\d+\.\d+\.\d+\.0$") {
    throw "Version '$Version' must use the four-component PowerToys release format."
}
if ($SourceCommit -notmatch "^[0-9a-fA-F]{40}$") {
    throw "SourceCommit must be a full immutable commit SHA."
}
if ($SourceBranch -notmatch "^refs/heads/.+") {
    throw "SourceBranch '$SourceBranch' is not a branch ref."
}
if ($Intent -ne "private-validation" -and $SourceBranch -notin @("refs/heads/main", "refs/heads/stable")) {
    throw "Intent '$Intent' is only supported from main or stable."
}

$publishPreview = switch ($ShouldPublishPreview.ToLowerInvariant()) {
    "true" { $true }
    "false" { $false }
    default { throw "ShouldPublishPreview must be true or false." }
}

if ($publishPreview -ne ($Intent -eq "preview-release")) {
    throw "ShouldPublishPreview '$publishPreview' conflicts with intent '$Intent'."
}
$expectedChannel = switch ($Intent) {
    "private-validation" { "private" }
    "preview-validation" { "preview" }
    "preview-release" { "preview" }
    "stable-release" { "stable" }
}
if ($Channel -ne $expectedChannel) {
    throw "Intent '$Intent' requires channel '$expectedChannel', not '$Channel'."
}

function ConvertTo-NullablePipelineValue {
    param([AllowEmptyString()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.StartsWith('$(')) {
        return $null
    }
    return $Value
}

$metadata = [ordered]@{
    schemaVersion = 1
    definitionId = $DefinitionId
    buildId = $BuildId
    buildNumber = $BuildNumber
    result = "succeeded"
    version = $Version
    channel = $Channel
    intent = $Intent
    sourceBranch = $SourceBranch
    sourceCommit = $SourceCommit.ToLowerInvariant()
    buildReason = $BuildReason
    queuedAt = ConvertTo-NullablePipelineValue -Value $QueuedAt
    startedAt = ConvertTo-NullablePipelineValue -Value $StartedAt
    finishedAt = $null
    shouldPublishPreview = $publishPreview
}

$parent = Split-Path -Parent $OutputPath
if ($parent) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
$metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath -Encoding utf8

[pscustomobject]$metadata
