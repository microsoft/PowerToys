<#
.SYNOPSIS
    Resolves and validates PowerToys preview-release metadata from an ADO build.

.DESCRIPTION
    Accepts a microsoft/Dart build URL or numeric build ID. The script prefers
    pipeline-published release-metadata.json, overlays immutable ADO build
    identity, and falls back to release-pipeline logs for older builds.

.EXAMPLE
    .\get-release-build-metadata.ps1 -Build 154000000 -OutputPath .\release-context.json

.EXAMPLE
    .\get-release-build-metadata.ps1 -Build 'https://microsoft.visualstudio.com/Dart/_build/results?buildId=154000000'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [string]$Build,

    [string]$Organization = "https://dev.azure.com/microsoft",
    [string]$Project = "Dart",

    [ValidateRange(1, [int]::MaxValue)]
    [int]$ExpectedDefinitionId = 76541,

    [string]$OutputPath,

    [string]$BuildJsonPath,
    [string]$ArtifactsJsonPath,
    [string]$MetadataJsonPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$env:AZURE_CORE_NO_PROMPT = "true"

. (Join-Path $PSScriptRoot "web-response-content.ps1")

$defaultExtensionDirectory = Join-Path $env:USERPROFILE ".azure\cliextensions"
if (-not $env:AZURE_EXTENSION_DIR -and (Test-Path -LiteralPath $defaultExtensionDirectory)) {
    $inaccessibleExtension = Get-ChildItem "$defaultExtensionDirectory\*\*.dist-info" -Directory -ErrorAction SilentlyContinue |
        Where-Object {
            try {
                [System.IO.Directory]::GetFiles($_.FullName) | Out-Null
                $false
            }
            catch {
                $true
            }
        } |
        Select-Object -First 1
    if ($inaccessibleExtension) {
        $cleanExtensionDirectory = Join-Path $env:USERPROFILE ".azure\cliextensions_clean"
        New-Item -ItemType Directory -Path $cleanExtensionDirectory -Force | Out-Null
        $env:AZURE_EXTENSION_DIR = $cleanExtensionDirectory
    }
}

function Resolve-BuildId {
    param([Parameter(Mandatory)][string]$Value)

    $trimmed = $Value.Trim()
    if ($trimmed -match "^\d+$") {
        return [int]::Parse($trimmed)
    }

    try {
        $uri = [Uri]$trimmed
    }
    catch {
        throw "Build must be a numeric build ID or a valid Azure DevOps build URL."
    }

    $match = [regex]::Match($uri.Query, "(?:^\?|&)buildId=(\d+)(?:&|$)", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) {
        throw "Azure DevOps URL does not contain a numeric buildId query parameter."
    }

    return [int]::Parse($match.Groups[1].Value)
}

function Invoke-Az {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)

    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI ('az') is required. Install it and run 'az login'."
    }

    $stderrPath = [System.IO.Path]::GetTempFileName()
    try {
        $output = & az @Arguments 2>$stderrPath
        $stderr = Get-Content -LiteralPath $stderrPath -Raw -ErrorAction SilentlyContinue
        if ($LASTEXITCODE -ne 0) {
            throw "az $($Arguments -join ' ') failed: $stderr"
        }
        return $output
    }
    finally {
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Get-AdoToken {
    $token = Invoke-Az account get-access-token `
        --resource "499b84ac-1321-427f-aa17-267ca6975798" `
        --query accessToken `
        --output tsv
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Failed to acquire an Azure DevOps access token. Run 'az login'."
    }
    return [string]$token
}

function Get-ArtifactFileUrl {
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string]$SubPath
    )

    $encodedSubPath = [Uri]::EscapeDataString($SubPath)
    $question = $BaseUrl.IndexOf("?")
    if ($question -lt 0) {
        return "${BaseUrl}?format=file&subPath=$encodedSubPath"
    }

    $base = $BaseUrl.Substring(0, $question)
    $parameters = $BaseUrl.Substring($question + 1) -split "&" | Where-Object {
        $_ -and $_ -notmatch "^(format|subPath)="
    }
    $parameters = @($parameters) + @("format=file", "subPath=$encodedSubPath")
    return "${base}?$($parameters -join '&')"
}

function Get-ArtifactMetadata {
    param(
        [Parameter(Mandatory)]$Artifacts,
        [Parameter(Mandatory)][string]$Token
    )

    $orderedArtifacts = @(
        $Artifacts |
            Where-Object { $_.name -in @("release-metadata", "build-x64-Release", "build-arm64-Release") } |
            Sort-Object @{
                Expression = {
                    switch ($_.name) {
                        "release-metadata" { 0 }
                        "build-x64-Release" { 1 }
                        default { 2 }
                    }
                }
            }
    )

    foreach ($artifact in $orderedArtifacts) {
        if (-not $artifact.resource -or -not $artifact.resource.downloadUrl) {
            continue
        }

        foreach ($subPath in @("/release-metadata.json", "release-metadata.json")) {
            $url = Get-ArtifactFileUrl -BaseUrl $artifact.resource.downloadUrl -SubPath $subPath
            try {
                $response = Invoke-WebRequest `
                    -Uri $url `
                    -Headers @{ Authorization = "Bearer $Token" } `
                    -TimeoutSec 15
                $text = ConvertFrom-WebResponseContent -Content $response.Content
                if (-not [string]::IsNullOrWhiteSpace($text)) {
                    return $text | ConvertFrom-Json
                }
            }
            catch {
                # Most artifacts do not contain this file. Continue to the next candidate.
            }
        }
    }

    return $null
}

function Get-LogMetadata {
    param(
        [Parameter(Mandatory)][int]$BuildId,
        [Parameter(Mandatory)][string]$Token,
        [Parameter(Mandatory)][string]$Organization,
        [Parameter(Mandatory)][string]$Project
    )

    $headers = @{ Authorization = "Bearer $Token" }
    $logUrls = @()
    $timelineUri = "$Organization/$Project/_apis/build/builds/$BuildId/timeline?api-version=7.1"
    try {
        $timeline = Invoke-RestMethod -Uri $timelineUri -Headers $headers -TimeoutSec 30
        $logUrls = @(
            $timeline.records |
                Where-Object {
                    $_.log -and $_.log.url -and
                    $_.name -in @("Prepare versioning", "Resolve symbol version")
                } |
                ForEach-Object { [string]$_.log.url } |
                Select-Object -Unique
        )
    }
    catch {
        $logUrls = @()
    }

    if ($logUrls.Count -eq 0) {
        $logsUri = "$Organization/$Project/_apis/build/builds/$BuildId/logs?api-version=7.1"
        $logs = Invoke-RestMethod -Uri $logsUri -Headers $headers -TimeoutSec 30
        $logUrls = @($logs.value | Sort-Object id -Descending | ForEach-Object { [string]$_.url })
    }

    $version = $null
    $channel = $null
    $intent = $null

    foreach ($logUrl in $logUrls) {
        if (-not $logUrl) {
            continue
        }

        try {
            $text = Invoke-RestMethod -Uri $logUrl -Headers $headers -TimeoutSec 30
            $joined = if ($text -is [array]) { $text -join "`n" } else { [string]$text }
            if (-not $version -and $joined -match "Resolved PowerToys version:\s*(\d+\.\d+\.\d+\.\d+)") {
                $version = $matches[1]
            }
            if (-not $channel -and $joined -match "Resolved release channel:\s*([a-z-]+)") {
                $channel = $matches[1]
            }
            if (-not $intent -and $joined -match "Resolved build intent:\s*([a-z-]+)") {
                $intent = $matches[1]
            }
            if ($version -and $channel -and $intent) {
                break
            }
        }
        catch {
            # A single inaccessible log is not fatal if another log has the metadata.
        }
    }

    if (-not $version -and -not $channel -and -not $intent) {
        return $null
    }

    return [pscustomobject]@{
        version = $version
        channel = $channel
        intent = $intent
        shouldPublishPreview = ($intent -eq "preview-release")
    }
}

function Get-PropertyValue {
    param(
        $Object,
        [Parameter(Mandatory)][string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }
    return $property.Value
}

function ConvertTo-Boolean {
    param($Value)

    if ($Value -is [bool]) {
        return $Value
    }
    if ($null -eq $Value) {
        return $false
    }
    return [string]$Value -eq "true"
}

$buildId = Resolve-BuildId -Value $Build

if ($BuildJsonPath) {
    $buildObject = Get-Content -LiteralPath $BuildJsonPath -Raw | ConvertFrom-Json
}
else {
    $extension = Invoke-Az extension list --query "[?name=='azure-devops'].name" --output tsv
    if ([string]::IsNullOrWhiteSpace($extension)) {
        Invoke-Az extension add --name azure-devops --yes --only-show-errors | Out-Null
    }
    Invoke-Az devops configure --defaults "organization=$Organization" "project=$Project" | Out-Null
    $buildObject = (Invoke-Az pipelines build show --id $buildId --output json) | ConvertFrom-Json
}

if ([int]$buildObject.id -ne $buildId) {
    throw "ADO returned build '$($buildObject.id)' while build '$buildId' was requested."
}

$artifacts = @()
if ($ArtifactsJsonPath) {
    $artifacts = @(Get-Content -LiteralPath $ArtifactsJsonPath -Raw | ConvertFrom-Json)
}
elseif (-not $MetadataJsonPath) {
    $artifacts = @((Invoke-Az pipelines runs artifact list --run-id $buildId --output json) | ConvertFrom-Json)
}

$pipelineMetadata = $null
$metadataSource = $null
$token = $null
if ($MetadataJsonPath) {
    $pipelineMetadata = Get-Content -LiteralPath $MetadataJsonPath -Raw | ConvertFrom-Json
    $metadataSource = "file"
}
elseif ($artifacts.Count -gt 0) {
    $token = Get-AdoToken
    $pipelineMetadata = Get-ArtifactMetadata -Artifacts $artifacts -Token $token
    if ($pipelineMetadata) {
        $metadataSource = "artifact"
    }
}

if (-not $pipelineMetadata) {
    if (-not $token) {
        $token = Get-AdoToken
    }
    $pipelineMetadata = Get-LogMetadata `
        -BuildId $buildId `
        -Token $token `
        -Organization $Organization `
        -Project $Project
    if ($pipelineMetadata) {
        $metadataSource = "pipeline-log"
    }
}

$templateParameters = Get-PropertyValue -Object $buildObject -Name "templateParameters"
$templateVersion = Get-PropertyValue -Object $templateParameters -Name "VersionNumber"
$version = Get-PropertyValue -Object $pipelineMetadata -Name "version"
if ([string]::IsNullOrWhiteSpace([string]$version)) {
    $version = $templateVersion
}

$definitionId = [int]$buildObject.definition.id
$sourceBranch = [string]$buildObject.sourceBranch
$sourceCommit = [string]$buildObject.sourceVersion
$result = [string]$buildObject.result
$channel = [string](Get-PropertyValue -Object $pipelineMetadata -Name "channel")
$intent = [string](Get-PropertyValue -Object $pipelineMetadata -Name "intent")
$shouldPublishPreview = ConvertTo-Boolean (Get-PropertyValue -Object $pipelineMetadata -Name "shouldPublishPreview")
$queuedAt = [string]$buildObject.queueTime

foreach ($field in @(
    @{ Name = "definitionId"; Build = $definitionId },
    @{ Name = "buildId"; Build = $buildId },
    @{ Name = "sourceBranch"; Build = $sourceBranch },
    @{ Name = "sourceCommit"; Build = $sourceCommit }
)) {
    $metadataValue = Get-PropertyValue -Object $pipelineMetadata -Name $field.Name
    if ($null -ne $metadataValue -and [string]$metadataValue -ne [string]$field.Build) {
        throw "Pipeline metadata $($field.Name) '$metadataValue' conflicts with ADO build value '$($field.Build)'."
    }
}

if ($definitionId -ne $ExpectedDefinitionId) {
    throw "Build $buildId uses definition $definitionId; expected trusted release definition $ExpectedDefinitionId."
}
if ($result -ne "succeeded") {
    throw "Build $buildId result is '$result'; only succeeded candidates are supported."
}
if ($sourceBranch -notin @("refs/heads/main", "refs/heads/stable")) {
    throw "Build $buildId source branch '$sourceBranch' is not main or stable."
}
if ($sourceCommit -notmatch "^[0-9a-fA-F]{40}$") {
    throw "Build $buildId does not identify a full immutable source commit."
}
if ([string]::IsNullOrWhiteSpace([string]$version) -or $version -notmatch "^\d+\.\d+\.\d+\.0$") {
    throw "Build $buildId release version could not be resolved uniquely."
}
if ([string]::IsNullOrWhiteSpace($queuedAt)) {
    throw "Build $buildId does not contain a queue timestamp."
}
try {
    [void]([datetime]::Parse($queuedAt, [Globalization.CultureInfo]::InvariantCulture))
}
catch {
    throw "Build $buildId queue timestamp '$queuedAt' is invalid."
}

$artifactNames = @($artifacts | ForEach-Object { [string]$_.name })
if ($artifactNames.Count -gt 0) {
    foreach ($requiredArtifact in @("build-x64-Release", "build-arm64-Release")) {
        if ($artifactNames -notcontains $requiredArtifact) {
            throw "Build $buildId is missing required artifact '$requiredArtifact'."
        }
    }
}

$context = [ordered]@{
    schemaVersion = 1
    metadataSource = $metadataSource
    organization = $Organization
    project = $Project
    definitionId = $definitionId
    buildId = $buildId
    buildUrl = "https://microsoft.visualstudio.com/$Project/_build/results?buildId=$buildId"
    buildNumber = [string]$buildObject.buildNumber
    result = $result
    version = [string]$version
    tag = "v$version"
    channel = $channel
    intent = $intent
    sourceBranch = $sourceBranch
    sourceCommit = $sourceCommit.ToLowerInvariant()
    buildReason = [string]$buildObject.reason
    queuedAt = $queuedAt
    startedAt = [string]$buildObject.startTime
    finishedAt = [string]$buildObject.finishTime
    shouldPublishPreview = $shouldPublishPreview
    artifactNames = $artifactNames
}

if ($OutputPath) {
    $parent = Split-Path -Parent $OutputPath
    if ($parent) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    $context | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding utf8
}

[pscustomobject]$context
