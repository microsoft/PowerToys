[CmdletBinding()]
param(
    [AllowEmptyString()]
    [string]$VersionOverride = "",

    [string]$SourceBranch = $env:BUILD_SOURCEBRANCH,

    [string]$BuildReason = $env:BUILD_REASON,

    [string]$BuildNumber = $env:BUILD_BUILDNUMBER,

    [string]$VersionPropsPath = (Join-Path $PSScriptRoot "..\src\Version.props")
)

$ErrorActionPreference = "Stop"

function Get-BuildStamp {
    param([string]$PipelineBuildNumber)

    if ([string]::IsNullOrWhiteSpace($PipelineBuildNumber)) {
        $now = Get-Date
        return [pscustomobject]@{
            YearMonth = $now.ToString("yyMM")
            Day = $now.ToString("dd")
            Revision = "001"
        }
    }

    if ($PipelineBuildNumber -notmatch "_(?<yearMonth>\d{4})\.(?<day>\d{2})(?<revision>\d{3})(?:-.+)?$") {
        throw "Build number '$PipelineBuildNumber' does not end with the expected _YYMM.DDNNN pattern"
    }

    $month = [int]::Parse($matches["yearMonth"].Substring(2, 2))
    $day = [int]::Parse($matches["day"])
    $revision = [int]::Parse($matches["revision"])
    if ($month -lt 1 -or $month -gt 12 -or $day -lt 1 -or $day -gt 31 -or $revision -lt 1) {
        throw "Build number '$PipelineBuildNumber' contains an invalid date or revision"
    }

    return [pscustomobject]@{
        YearMonth = $matches["yearMonth"]
        Day = $matches["day"]
        Revision = $matches["revision"]
    }
}

function Test-VersionParts {
    param([Parameter(Mandatory)][string[]]$Parts)

    foreach ($part in $Parts) {
        $value = [int]::Parse($part)
        if ($value -lt 0 -or $value -gt [UInt16]::MaxValue) {
            throw "Version component '$value' is outside the supported Windows version range 0-65535"
        }
    }
}

function Get-ReleaseTrain {
    param([Parameter(Mandatory)][string]$Path)

    [xml]$versionProps = Get-Content -LiteralPath $Path
    $releaseTrain = [string]$versionProps.Project.PropertyGroup.ReleaseTrainVersion
    if ($releaseTrain -notmatch "^(?<major>\d+)\.(?<minor>\d+)$") {
        throw "ReleaseTrainVersion in '$Path' must use the major.minor format"
    }

    Test-VersionParts -Parts @($matches["major"], $matches["minor"])
    return $releaseTrain
}

function Get-PreviewVersion {
    param(
        [Parameter(Mandatory)][string]$ReleaseTrain,
        [AllowEmptyString()][string]$Override,
        [Parameter(Mandatory)]$BuildStamp
    )

    $inputVersion = $Override.Trim()
    if ($inputVersion.EndsWith("-preview", [StringComparison]::OrdinalIgnoreCase)) {
        $inputVersion = $inputVersion.Substring(0, $inputVersion.Length - "-preview".Length)
    }

    if ([string]::IsNullOrWhiteSpace($inputVersion)) {
        $inputVersion = $ReleaseTrain
    }

    if ($inputVersion -match "^(?<major>\d+)\.(?<minor>\d+)$") {
        if ($inputVersion -ne $ReleaseTrain) {
            throw "Preview version base '$inputVersion' does not match ReleaseTrainVersion '$ReleaseTrain'"
        }

        $build = [int]::Parse("$($BuildStamp.Day)$($BuildStamp.Revision)")
        return "$inputVersion.$($BuildStamp.YearMonth).$build"
    }

    if ($inputVersion -notmatch "^(?<major>\d+)\.(?<minor>\d+)\.(?<yearMonth>\d{4})\.(?<dailyBuild>\d{5})$") {
        throw "Preview version override must be major.minor or major.minor.YYMM.DDNNN, optionally followed by -preview"
    }

    if ("$($matches["major"]).$($matches["minor"])" -ne $ReleaseTrain) {
        throw "Preview version '$inputVersion' does not match ReleaseTrainVersion '$ReleaseTrain'"
    }

    $month = [int]::Parse($matches["yearMonth"].Substring(2, 2))
    $day = [int]::Parse($matches["dailyBuild"].Substring(0, 2))
    $revision = [int]::Parse($matches["dailyBuild"].Substring(2, 3))
    if ($month -lt 1 -or $month -gt 12 -or $day -lt 1 -or $day -gt 31 -or $revision -lt 1) {
        throw "Preview version '$inputVersion' contains an invalid YYMM or DDNNN component"
    }

    Test-VersionParts -Parts @($matches["major"], $matches["minor"], $matches["yearMonth"], $matches["dailyBuild"])
    return $inputVersion
}

function Get-StableVersion {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Override)

    $inputVersion = $Override.Trim()
    if ($inputVersion -notmatch "^(?<major>\d+)\.(?<minor>\d+)\.(?<revision>\d+)(?:\.(?<build>\d+))?$") {
        throw "A manually queued stable build requires a numeric major.minor.patch or major.minor.patch.build version override"
    }

    $parts = @($matches["major"], $matches["minor"], $matches["revision"])
    if ($matches["build"]) {
        $parts += $matches["build"]
    }

    Test-VersionParts -Parts $parts
    return ($parts | ForEach-Object { [int]::Parse($_) }) -join "."
}

$isMain = $SourceBranch -eq "refs/heads/main"
$isStable = $SourceBranch -eq "refs/heads/stable"
$isScheduled = $BuildReason -eq "Schedule"

if ($isScheduled -and -not $isMain) {
    throw "Scheduled release builds are only supported from refs/heads/main"
}

$releaseTrain = Get-ReleaseTrain -Path $VersionPropsPath
$buildStamp = Get-BuildStamp -PipelineBuildNumber $BuildNumber

if ($isMain) {
    if ($isScheduled -and -not [string]::IsNullOrWhiteSpace($VersionOverride)) {
        throw "Scheduled main builds must use the checked-in ReleaseTrainVersion and cannot specify a version override"
    }

    $intent = if ($isScheduled) { "preview-release" } else { "preview-validation" }
    $channel = "preview"
    $version = Get-PreviewVersion -ReleaseTrain $releaseTrain -Override $VersionOverride -BuildStamp $buildStamp
    $allowPublicSymbols = $false
    $shouldPublishPreview = $isScheduled
}
elseif ($isStable) {
    if ($isScheduled) {
        throw "Stable release builds must be queued manually"
    }

    $intent = "stable-release"
    $channel = "stable"
    $version = Get-StableVersion -Override $VersionOverride
    $allowPublicSymbols = $true
    $shouldPublishPreview = $false
}
else {
    if (-not [string]::IsNullOrWhiteSpace($VersionOverride)) {
        throw "Version overrides are not supported for private branch builds"
    }

    $intent = "private-validation"
    $channel = "private"
    $build = [int]::Parse("$($buildStamp.Day)$($buildStamp.Revision)")
    $version = "0.0.$($buildStamp.YearMonth).$build"
    $allowPublicSymbols = $false
    $shouldPublishPreview = $false
}

Test-VersionParts -Parts ($version -split "\.")

Write-Host "Resolved build intent: $intent"
Write-Host "Resolved release channel: $channel"
Write-Host "Resolved version: $version"

[pscustomobject]@{
    Intent = $intent
    Channel = $channel
    Version = $version
    AllowPublicSymbols = $allowPublicSymbols
    ShouldPublishPreview = $shouldPublishPreview
}
