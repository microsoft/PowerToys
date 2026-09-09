[CmdletBinding()]
param(
    [AllowEmptyString()]
    [string]$VersionOverride = "",

    [ValidateSet("auto", "preview-release", "stable-release")]
    [string]$ReleaseIntent = "auto",

    [string]$SourceBranch = $env:BUILD_SOURCEBRANCH,

    [string]$BuildReason = $env:BUILD_REASON,

    [string]$BuildNumber = $env:BUILD_BUILDNUMBER,

    [AllowEmptyString()]
    [string]$BuildDate = "",

    [AllowEmptyString()]
    [string]$DailyVersionSequence = "",

    [string]$VersionPropsPath = (Join-Path $PSScriptRoot "..\src\Version.props")
)

$ErrorActionPreference = "Stop"

$VersionOverride = $VersionOverride.Trim()
if ($VersionOverride.Equals("auto", [StringComparison]::OrdinalIgnoreCase)) {
    $VersionOverride = ""
}

function Get-BuildStamp {
    param([string]$PipelineBuildNumber)

    if ([string]::IsNullOrWhiteSpace($PipelineBuildNumber)) {
        $now = Get-Date
        return [pscustomobject]@{
            Date = $now.Date
            Revision = 1
        }
    }

    if ($PipelineBuildNumber -notmatch "_(?<yearMonth>\d{4})\.(?<day>\d{2})(?<revision>\d{3})(?:-.+)?$") {
        throw "Build number '$PipelineBuildNumber' does not end with the expected _YYMM.DDNNN pattern"
    }

    try {
        $date = [datetime]::ParseExact(
            "20$($matches["yearMonth"])$($matches["day"])",
            "yyyyMMdd",
            [Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        throw "Build number '$PipelineBuildNumber' contains an invalid date"
    }

    $revision = [int]::Parse($matches["revision"])
    if ($revision -lt 1 -or $revision -gt 99) {
        throw "Build number '$PipelineBuildNumber' has daily revision '$revision'; canonical versions support revisions 001 through 099"
    }

    return [pscustomobject]@{
        Date = $date
        Revision = $revision
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

function Get-VersionDate {
    param(
        [AllowEmptyString()][string]$DateOverride,
        [Parameter(Mandatory)]$BuildStamp
    )

    if ([string]::IsNullOrWhiteSpace($DateOverride)) {
        return $BuildStamp.Date
    }

    try {
        return [datetime]::ParseExact(
            $DateOverride,
            "yyyyMMdd",
            [Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        throw "Build date '$DateOverride' must use the yyyyMMdd format"
    }
}

function Get-ReleaseTrainMetadata {
    param([Parameter(Mandatory)][string]$Path)

    [xml]$versionProps = Get-Content -LiteralPath $Path
    $releaseTrain = [string]$versionProps.Project.PropertyGroup.ReleaseTrainVersion
    if ($releaseTrain -notmatch "^(?<major>\d+)\.(?<minor>\d+)$") {
        throw "ReleaseTrainVersion in '$Path' must use the major.minor format"
    }

    Test-VersionParts -Parts @($matches["major"], $matches["minor"])
    if ([int]::Parse($matches["major"]) -gt 255 -or [int]::Parse($matches["minor"]) -gt 255) {
        throw "ReleaseTrainVersion in '$Path' must keep major and minor within the MSI-supported range 0-255"
    }

    $epochText = [string]$versionProps.Project.PropertyGroup.ReleaseTrainEpoch
    try {
        $epoch = [datetime]::ParseExact(
            $epochText,
            "yyyy-MM-dd",
            [Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        throw "ReleaseTrainEpoch in '$Path' must use the yyyy-MM-dd format"
    }

    if ($epoch.Month -ne 1 -or $epoch.Day -ne 1) {
        throw "ReleaseTrainEpoch in '$Path' must be January 1 of the active epoch year"
    }

    return [pscustomobject]@{
        Version = $releaseTrain
        Epoch = $epoch
    }
}

function Get-ReleaseVersion {
    param(
        [Parameter(Mandatory)][string]$ReleaseTrain,
        [Parameter(Mandatory)][datetime]$Epoch,
        [Parameter(Mandatory)]$BuildStamp,
        [Parameter(Mandatory)][int]$DailySequence
    )

    if ($BuildStamp.Date -lt $Epoch) {
        throw "Build date '$($BuildStamp.Date.ToString("yyyy-MM-dd"))' is before ReleaseTrainEpoch '$($Epoch.ToString("yyyy-MM-dd"))'"
    }

    if ($DailySequence -lt 1 -or $DailySequence -gt 9) {
        throw "Daily release sequence '$DailySequence' is outside the YDDDB-supported range 1-9"
    }

    $yearOffset = $BuildStamp.Date.Year - $Epoch.Year
    if ($yearOffset -gt 6) {
        throw "Release train year offset '$yearOffset' exceeds the MSI-safe YDDDB range 0-6; advance the release train and reset ReleaseTrainEpoch"
    }

    $thirdComponentText = "{0}{1:D3}{2}" -f $yearOffset, $BuildStamp.Date.DayOfYear, $DailySequence
    $thirdComponent = [int]::Parse($thirdComponentText)
    if ($thirdComponent -gt [UInt16]::MaxValue) {
        throw "Generated version component '$thirdComponent' exceeds 65535; advance the release train and reset ReleaseTrainEpoch"
    }

    return "$ReleaseTrain.$thirdComponent.0"
}

function Get-PrivateVersion {
    param(
        [Parameter(Mandatory)][datetime]$Epoch,
        [Parameter(Mandatory)]$BuildStamp
    )

    $extendedDay = ($BuildStamp.Date - $Epoch).Days + 1
    if ($extendedDay -lt 1) {
        throw "Build date '$($BuildStamp.Date.ToString("yyyy-MM-dd"))' is before ReleaseTrainEpoch '$($Epoch.ToString("yyyy-MM-dd"))'"
    }

    $thirdComponent = ($extendedDay * 100) + $BuildStamp.Revision
    if ($thirdComponent -gt [UInt16]::MaxValue) {
        throw "Generated private version component '$thirdComponent' exceeds 65535"
    }

    return "0.0.$thirdComponent.0"
}

function Get-ReleaseDailySequence {
    param([AllowEmptyString()][string]$Sequence)

    if ([string]::IsNullOrWhiteSpace($Sequence)) {
        throw "DailyVersionSequence is required for main and stable builds"
    }

    if ($Sequence -notmatch "^\d+$") {
        throw "Daily release sequence '$Sequence' must be numeric"
    }

    return [int]::Parse($Sequence)
}

function Get-AutomaticReleaseVersion {
    param(
        [Parameter(Mandatory)][string]$ReleaseTrain,
        [Parameter(Mandatory)][datetime]$Epoch,
        [Parameter(Mandatory)]$BuildStamp,
        [Parameter(Mandatory)][AllowEmptyString()][string]$DateOverride,
        [Parameter(Mandatory)][AllowEmptyString()][string]$DailySequence
    )

    $releaseBuildStamp = [pscustomobject]@{
        Date = Get-VersionDate -DateOverride $DateOverride -BuildStamp $BuildStamp
        Revision = $BuildStamp.Revision
    }
    $releaseDailySequence = Get-ReleaseDailySequence -Sequence $DailySequence
    return Get-ReleaseVersion -ReleaseTrain $ReleaseTrain -Epoch $Epoch -BuildStamp $releaseBuildStamp -DailySequence $releaseDailySequence
}

function Get-PreviewVersionOverride {
    param(
        [Parameter(Mandatory)][string]$ReleaseTrain,
        [AllowEmptyString()][string]$Override
    )

    $inputVersion = $Override.Trim()
    if ($inputVersion.EndsWith("-preview", [StringComparison]::OrdinalIgnoreCase)) {
        $inputVersion = $inputVersion.Substring(0, $inputVersion.Length - "-preview".Length)
    }

    if ([string]::IsNullOrWhiteSpace($inputVersion)) {
        return $null
    }

    if ($inputVersion -match "^(?<major>\d+)\.(?<minor>\d+)$") {
        if ($inputVersion -ne $ReleaseTrain) {
            throw "Preview version base '$inputVersion' does not match ReleaseTrainVersion '$ReleaseTrain'"
        }

        return $null
    }

    if ($inputVersion -notmatch "^(?<major>\d+)\.(?<minor>\d+)\.(?<revision>\d+)\.(?<build>\d+)$") {
        throw "Preview version override must be major.minor or major.minor.YDDDB.0, optionally followed by -preview"
    }

    if ("$($matches["major"]).$($matches["minor"])" -ne $ReleaseTrain) {
        throw "Preview version '$inputVersion' does not match ReleaseTrainVersion '$ReleaseTrain'"
    }

    if ([int]::Parse($matches["build"]) -ne 0) {
        throw "Preview version '$inputVersion' must use 0 for the fourth component"
    }

    Test-VersionParts -Parts @($matches["major"], $matches["minor"], $matches["revision"], $matches["build"])
    $parts = @($matches["major"], $matches["minor"], $matches["revision"], $matches["build"])
    return ($parts | ForEach-Object { [int]::Parse($_) }) -join "."
}

function Get-MsiSafeVersionOverride {
    param(
        [Parameter(Mandatory)][string]$Override,
        [Parameter(Mandatory)][string]$VersionKind
    )

    $inputVersion = $Override.Trim()
    if ($inputVersion -notmatch "^(?<major>\d+)\.(?<minor>\d+)\.(?<revision>\d+)(?:\.(?<build>\d+))?$") {
        throw "$VersionKind version override must be numeric major.minor.patch or major.minor.patch.build"
    }

    $parts = @($matches["major"], $matches["minor"], $matches["revision"])
    if ($matches["build"]) {
        $parts += $matches["build"]
    }
    else {
        $parts += "0"
    }

    Test-VersionParts -Parts $parts
    if ([int]::Parse($parts[0]) -gt 255 -or [int]::Parse($parts[1]) -gt 255) {
        throw "$VersionKind version '$inputVersion' must keep major and minor within the MSI-supported range 0-255"
    }

    if ([int]::Parse($parts[3]) -ne 0) {
        throw "$VersionKind version '$inputVersion' must use 0 for the fourth component"
    }

    return ($parts | ForEach-Object { [int]::Parse($_) }) -join "."
}

function Get-StableVersionOverride {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Override
    )

    if ([string]::IsNullOrWhiteSpace($Override)) {
        return $null
    }

    return Get-MsiSafeVersionOverride -Override $Override -VersionKind "Stable"
}

$isMain = $SourceBranch -eq "refs/heads/main"
$isStable = $SourceBranch -eq "refs/heads/stable"
$isScheduled = $BuildReason -eq "Schedule"

if ($ReleaseIntent -eq "stable-release" -and -not $isStable) {
    throw "Stable release intent is only supported from refs/heads/stable"
}
if ($ReleaseIntent -eq "preview-release" -and -not ($isMain -or $isStable)) {
    throw "Preview release intent is only supported from refs/heads/main or refs/heads/stable"
}
if ($isScheduled -and $isStable -and $ReleaseIntent -ne "preview-release") {
    throw "Scheduled stable builds must explicitly use preview-release intent"
}
if ($isScheduled -and -not ($isMain -or $isStable)) {
    throw "Scheduled release builds are only supported from refs/heads/main or refs/heads/stable"
}

$releaseMetadata = Get-ReleaseTrainMetadata -Path $VersionPropsPath
$releaseTrain = $releaseMetadata.Version
$buildStamp = Get-BuildStamp -PipelineBuildNumber $BuildNumber

if ($isMain) {
    if ($isScheduled -and -not [string]::IsNullOrWhiteSpace($VersionOverride)) {
        throw "Scheduled main builds must use the checked-in ReleaseTrainVersion and cannot specify a version override"
    }

    $intent = if ($isScheduled -or $ReleaseIntent -eq "preview-release") { "preview-release" } else { "preview-validation" }
    $channel = "preview"
    $version = Get-PreviewVersionOverride -ReleaseTrain $releaseTrain -Override $VersionOverride
    if ($null -eq $version) {
        $version = Get-AutomaticReleaseVersion `
            -ReleaseTrain $releaseTrain `
            -Epoch $releaseMetadata.Epoch `
            -BuildStamp $buildStamp `
            -DateOverride $BuildDate `
            -DailySequence $DailyVersionSequence
    }
    $allowPublicSymbols = $false
    $shouldPublishPreview = $intent -eq "preview-release"
}
elseif ($isStable) {
    if ($ReleaseIntent -eq "preview-release") {
        $intent = "preview-release"
        $channel = "preview"
        $version = Get-PreviewVersionOverride -ReleaseTrain $releaseTrain -Override $VersionOverride
        $allowPublicSymbols = $false
        $shouldPublishPreview = $true
    }
    else {
        $intent = "stable-release"
        $channel = "stable"
        $version = Get-StableVersionOverride -Override $VersionOverride
        $allowPublicSymbols = $true
        $shouldPublishPreview = $false
    }

    if ($null -eq $version) {
        $version = Get-AutomaticReleaseVersion `
            -ReleaseTrain $releaseTrain `
            -Epoch $releaseMetadata.Epoch `
            -BuildStamp $buildStamp `
            -DateOverride $BuildDate `
            -DailySequence $DailyVersionSequence
    }
}
else {
    $intent = "private-validation"
    $channel = "private"
    $version = if ([string]::IsNullOrWhiteSpace($VersionOverride)) {
        Get-PrivateVersion -Epoch $releaseMetadata.Epoch -BuildStamp $buildStamp
    }
    else {
        Get-MsiSafeVersionOverride -Override $VersionOverride -VersionKind "Private"
    }
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
