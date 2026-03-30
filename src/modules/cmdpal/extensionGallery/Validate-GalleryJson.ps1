# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [string]$GalleryRoot = $PSScriptRoot,
    [switch]$TreatWarningsAsErrors
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:galleryRootPath = $null
$script:validationIssues = [System.Collections.Generic.List[object]]::new()
$script:stats = [ordered]@{
    IndexEntriesValidated       = 0
    ExtensionDirectoriesFound   = 0
    ManifestsValidated          = 0
    LocalizedManifestsValidated = 0
}

function Add-Issue {
    param(
        [ValidateSet('ERROR', 'WARN')]
        [string]$Severity,
        [string]$Scope,
        [string]$Message
    )

    $script:validationIssues.Add([pscustomobject]@{
            Severity = $Severity
            Scope    = $Scope
            Message  = $Message
        })
}

function Get-DisplayPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return '<unknown>'
    }

    try {
        if ([System.IO.Path]::IsPathRooted($Path) -and -not [string]::IsNullOrWhiteSpace($script:galleryRootPath)) {
            return [System.IO.Path]::GetRelativePath($script:galleryRootPath, $Path)
        }
    } catch {
        # Fall through to raw path.
    }

    return $Path
}

function Add-ValidationError {
    param(
        [string]$Scope,
        [string]$Message
    )

    Add-Issue -Severity 'ERROR' -Scope $Scope -Message $Message
}

function Add-ValidationWarning {
    param(
        [string]$Scope,
        [string]$Message
    )

    Add-Issue -Severity 'WARN' -Scope $Scope -Message $Message
}

function Test-NonEmptyString {
    param([object]$Value)

    return $Value -is [string] -and -not [string]::IsNullOrWhiteSpace($Value)
}

function Test-ValidUri {
    param([object]$Value)

    if (-not ($Value -is [string])) {
        return $false
    }

    $uri = $null
    return [System.Uri]::TryCreate($Value, [System.UriKind]::Absolute, [ref]$uri)
}

function Test-IdFormat {
    param([string]$Id)

    return $Id -match '^[a-z0-9-]+$'
}

function Get-JsonDocument {
    param(
        [string]$Path,
        [string]$Scope
    )

    if (-not (Test-Path -Path $Path -PathType Leaf)) {
        Add-ValidationError -Scope $Scope -Message ("File not found: {0}" -f (Get-DisplayPath -Path $Path))
        return $null
    }

    try {
        return (Get-Content -Path $Path -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable -Depth 100)
    } catch {
        Add-ValidationError -Scope $Scope -Message ("Invalid JSON in '{0}'. {1}" -f (Get-DisplayPath -Path $Path), $_.Exception.Message)
        return $null
    }
}

function Validate-AllowedProperties {
    param(
        [hashtable]$Object,
        [string[]]$AllowedProperties,
        [string]$Scope
    )

    for ($i = 0; $i -lt $Object.Keys.Count; $i++) {
        $key = [string]$Object.Keys[$i]
        if ($AllowedProperties -notcontains $key) {
            Add-ValidationError -Scope $Scope -Message ("Unknown property '{0}'." -f $key)
        }
    }
}

function Validate-Tags {
    param(
        [object]$TagsValue,
        [string]$Scope
    )

    if ($null -eq $TagsValue) {
        return @()
    }

    if ($TagsValue -is [string] -or $TagsValue -isnot [System.Collections.IEnumerable]) {
        Add-ValidationError -Scope $Scope -Message "'tags' must be an array of non-empty strings."
        return @()
    }

    $tags = @($TagsValue)
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $normalized = [System.Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $tags.Count; $i++) {
        $tag = $tags[$i]
        if (-not (Test-NonEmptyString -Value $tag)) {
            Add-ValidationError -Scope $Scope -Message ("tags[{0}] must be a non-empty string." -f $i)
            continue
        }

        $trimmedTag = $tag.Trim()
        if (-not $seen.Add($trimmedTag)) {
            Add-ValidationError -Scope $Scope -Message ("Duplicate tag '{0}'." -f $trimmedTag)
            continue
        }

        $normalized.Add($trimmedTag)
    }

    return @($normalized)
}

function Validate-InstallSources {
    param(
        [object]$InstallSources,
        [string]$Scope
    )

    if ($InstallSources -is [string] -or $InstallSources -isnot [System.Collections.IEnumerable]) {
        Add-ValidationError -Scope $Scope -Message "'installSources' must be an array."
        return
    }

    $sources = @($InstallSources)
    if ($sources.Count -lt 1) {
        Add-ValidationError -Scope $Scope -Message "'installSources' must contain at least one source."
        return
    }

    $allowedSourceTypes = @('winget', 'msstore', 'url')
    $allowedSourceProperties = @('type', 'id', 'uri')

    for ($i = 0; $i -lt $sources.Count; $i++) {
        $sourceScope = "{0} installSources[{1}]" -f $Scope, $i
        $source = $sources[$i]
        if ($source -isnot [hashtable]) {
            Add-ValidationError -Scope $sourceScope -Message "Source entry must be an object."
            continue
        }

        Validate-AllowedProperties -Object $source -AllowedProperties $allowedSourceProperties -Scope $sourceScope

        if (-not (Test-NonEmptyString -Value $source['type'])) {
            Add-ValidationError -Scope $sourceScope -Message "'type' is required."
            continue
        }

        $sourceType = $source['type'].Trim().ToLowerInvariant()
        if ($allowedSourceTypes -notcontains $sourceType) {
            Add-ValidationError -Scope $sourceScope -Message ("Unsupported source type '{0}'." -f $sourceType)
            continue
        }

        if (($sourceType -eq 'winget' -or $sourceType -eq 'msstore') -and -not (Test-NonEmptyString -Value $source['id'])) {
            Add-ValidationError -Scope $sourceScope -Message ("'{0}' source requires a non-empty 'id'." -f $sourceType)
        }

        if ($sourceType -eq 'url' -and -not (Test-ValidUri -Value $source['uri'])) {
            Add-ValidationError -Scope $sourceScope -Message "'url' source requires a valid absolute 'uri'."
        }

        if ($null -ne $source['uri'] -and -not (Test-ValidUri -Value $source['uri'])) {
            Add-ValidationError -Scope $sourceScope -Message "'uri' must be a valid absolute URI when provided."
        }
    }
}

function Validate-Detection {
    param(
        [object]$DetectionValue,
        [string]$Scope
    )

    if ($null -eq $DetectionValue) {
        return
    }

    if ($DetectionValue -isnot [hashtable]) {
        Add-ValidationError -Scope $Scope -Message "'detection' must be an object when provided."
        return
    }

    $allowedDetectionProperties = @('packageFamilyName')
    Validate-AllowedProperties -Object $DetectionValue -AllowedProperties $allowedDetectionProperties -Scope $Scope

    if ($DetectionValue.ContainsKey('packageFamilyName') -and $null -ne $DetectionValue['packageFamilyName'] -and -not (Test-NonEmptyString -Value $DetectionValue['packageFamilyName'])) {
        Add-ValidationError -Scope $Scope -Message "detection.packageFamilyName must be a non-empty string when provided."
    }
}

function Validate-OptionalFileField {
    param(
        [hashtable]$Manifest,
        [string]$PropertyName,
        [string]$ExtensionDirectory,
        [string]$Scope
    )

    if (-not $Manifest.ContainsKey($PropertyName) -or $null -eq $Manifest[$PropertyName]) {
        return
    }

    if (-not (Test-NonEmptyString -Value $Manifest[$PropertyName])) {
        Add-ValidationError -Scope $Scope -Message ("'{0}' must be a non-empty string when provided." -f $PropertyName)
        return
    }

    $fileName = [string]$Manifest[$PropertyName]
    $filePath = Join-Path -Path $ExtensionDirectory -ChildPath $fileName
    if (-not (Test-Path -Path $filePath -PathType Leaf)) {
        Add-ValidationError -Scope $Scope -Message ("{0} file '{1}' was not found in '{2}'." -f $PropertyName, $fileName, (Get-DisplayPath -Path $ExtensionDirectory))
    }
}

function Validate-Manifest {
    param(
        [hashtable]$Manifest,
        [string]$ManifestPath,
        [string]$ExpectedId,
        [string]$ExtensionDirectory
    )

    $scope = Get-DisplayPath -Path $ManifestPath

    if ($Manifest -isnot [hashtable]) {
        Add-ValidationError -Scope $scope -Message "Manifest root must be a JSON object."
        return
    }

    $allowedManifestProperties = @('$schema', 'id', 'title', 'description', 'author', 'homepage', 'readme', 'icon', 'iconDark', 'tags', 'installSources', 'detection')
    Validate-AllowedProperties -Object $Manifest -AllowedProperties $allowedManifestProperties -Scope $scope

    if (-not (Test-NonEmptyString -Value $Manifest['id'])) {
        Add-ValidationError -Scope $scope -Message "'id' is required."
    } else {
        $id = [string]$Manifest['id']
        $trimmedId = $id.Trim()
        if (-not (Test-IdFormat -Id $trimmedId)) {
            Add-ValidationError -Scope $scope -Message "'id' must match ^[a-z0-9-]+$."
        }

        if ($trimmedId -ne $ExpectedId) {
            Add-ValidationError -Scope $scope -Message ("Manifest id '{0}' does not match extension id '{1}'." -f $trimmedId, $ExpectedId)
        }
    }

    if (-not (Test-NonEmptyString -Value $Manifest['title'])) {
        Add-ValidationError -Scope $scope -Message "'title' is required and must be a non-empty string."
    }

    if (-not (Test-NonEmptyString -Value $Manifest['description'])) {
        Add-ValidationError -Scope $scope -Message "'description' is required and must be a non-empty string."
    }

    if ($Manifest.ContainsKey('homepage') -and $null -ne $Manifest['homepage'] -and -not (Test-ValidUri -Value $Manifest['homepage'])) {
        Add-ValidationError -Scope $scope -Message "'homepage' must be a valid absolute URI when provided."
    }

    if (-not $Manifest.ContainsKey('author') -or $Manifest['author'] -isnot [hashtable]) {
        Add-ValidationError -Scope $scope -Message "'author' is required and must be an object."
    } else {
        $author = [hashtable]$Manifest['author']
        Validate-AllowedProperties -Object $author -AllowedProperties @('name', 'url') -Scope ("{0} author" -f $scope)

        if (-not (Test-NonEmptyString -Value $author['name'])) {
            Add-ValidationError -Scope $scope -Message "author.name is required."
        }

        if ($author.ContainsKey('url') -and $null -ne $author['url'] -and -not (Test-ValidUri -Value $author['url'])) {
            Add-ValidationError -Scope $scope -Message "author.url must be a valid absolute URI when provided."
        }
    }

    Validate-OptionalFileField -Manifest $Manifest -PropertyName 'readme' -ExtensionDirectory $ExtensionDirectory -Scope $scope
    Validate-OptionalFileField -Manifest $Manifest -PropertyName 'icon' -ExtensionDirectory $ExtensionDirectory -Scope $scope
    Validate-OptionalFileField -Manifest $Manifest -PropertyName 'iconDark' -ExtensionDirectory $ExtensionDirectory -Scope $scope

    Validate-Tags -TagsValue $Manifest['tags'] -Scope $scope | Out-Null
    Validate-InstallSources -InstallSources $Manifest['installSources'] -Scope $scope
    Validate-Detection -DetectionValue $Manifest['detection'] -Scope ("{0} detection" -f $scope)
}

function Write-ValidationHeader {
    param(
        [string]$RootPath,
        [string]$IndexPath,
        [string]$SchemaPath
    )

    Write-Host ""
    Write-Host "Command Palette Gallery JSON Validation" -ForegroundColor Cyan
    Write-Host ("Root   : {0}" -f $RootPath) -ForegroundColor DarkGray
    Write-Host ("Index  : {0}" -f $IndexPath) -ForegroundColor DarkGray
    Write-Host ("Schema : {0}" -f $SchemaPath) -ForegroundColor DarkGray
    Write-Host ("Time   : {0}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss")) -ForegroundColor DarkGray
}

function Write-IssuesBySeverity {
    param(
        [string]$Severity,
        [string]$Title,
        [ConsoleColor]$Color
    )

    $matchingIssues = @($script:validationIssues | Where-Object { $_.Severity -eq $Severity } | Sort-Object Scope, Message)
    if ($matchingIssues.Count -eq 0) {
        return
    }

    Write-Host ""
    Write-Host ("{0} ({1})" -f $Title, $matchingIssues.Count) -ForegroundColor $Color

    $currentScope = $null
    for ($i = 0; $i -lt $matchingIssues.Count; $i++) {
        $issue = $matchingIssues[$i]
        if ($issue.Scope -ne $currentScope) {
            Write-Host ("  [{0}]" -f $issue.Scope) -ForegroundColor DarkGray
            $currentScope = $issue.Scope
        }

        Write-Host ("    - {0}" -f $issue.Message) -ForegroundColor $Color
    }
}

function Write-ValidationSummary {
    $errorCount = @($script:validationIssues | Where-Object { $_.Severity -eq 'ERROR' }).Count
    $warningCount = @($script:validationIssues | Where-Object { $_.Severity -eq 'WARN' }).Count

    Write-Host ""
    Write-Host "Validation Summary" -ForegroundColor Cyan
    Write-Host ("  Index entries validated        : {0}" -f $script:stats.IndexEntriesValidated)
    Write-Host ("  Extension directories found    : {0}" -f $script:stats.ExtensionDirectoriesFound)
    Write-Host ("  Manifest files validated       : {0}" -f $script:stats.ManifestsValidated)
    Write-Host ("  Localized manifests validated  : {0}" -f $script:stats.LocalizedManifestsValidated)
    Write-Host ("  Errors                         : {0}" -f $errorCount)
    Write-Host ("  Warnings                       : {0}" -f $warningCount)

    if ($errorCount -eq 0 -and ($warningCount -eq 0 -or -not $TreatWarningsAsErrors)) {
        Write-Host ""
        if ($warningCount -gt 0) {
            Write-Host "Result: PASS (with warnings)" -ForegroundColor Yellow
        } else {
            Write-Host "Result: PASS" -ForegroundColor Green
        }
    } else {
        Write-Host ""
        if ($TreatWarningsAsErrors -and $warningCount -gt 0 -and $errorCount -eq 0) {
            Write-Host "Result: FAIL (warnings treated as errors)" -ForegroundColor Red
        } else {
            Write-Host "Result: FAIL" -ForegroundColor Red
        }
    }
}

$script:galleryRootPath = (Resolve-Path -Path $GalleryRoot).Path
$indexPath = Join-Path -Path $script:galleryRootPath -ChildPath 'index.json'
$schemaPath = Join-Path -Path $script:galleryRootPath -ChildPath 'schema.json'
$extensionsRoot = Join-Path -Path $script:galleryRootPath -ChildPath 'extensions'

Write-ValidationHeader -RootPath $script:galleryRootPath -IndexPath (Get-DisplayPath -Path $indexPath) -SchemaPath (Get-DisplayPath -Path $schemaPath)

if (-not (Test-Path -Path $extensionsRoot -PathType Container)) {
    Add-ValidationError -Scope "extensions" -Message ("Directory not found: {0}" -f (Get-DisplayPath -Path $extensionsRoot))
}

$null = Get-JsonDocument -Path $schemaPath -Scope 'schema.json'
$indexDocument = Get-JsonDocument -Path $indexPath -Scope 'index.json'

$indexEntries = [System.Collections.Generic.List[hashtable]]::new()
$indexIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

if ($null -ne $indexDocument) {
    if ($indexDocument -is [string] -or $indexDocument -isnot [System.Collections.IEnumerable]) {
        Add-ValidationError -Scope 'index.json' -Message "index.json root must be an array."
    } else {
        $rawEntries = @($indexDocument)
        for ($i = 0; $i -lt $rawEntries.Count; $i++) {
            $entryScope = ("index.json[{0}]" -f $i)
            $entry = $rawEntries[$i]
            $entryId = $null
            $entryTags = @()

            if ($entry -is [string]) {
                $entryId = $entry
            } elseif ($entry -is [hashtable]) {
                Validate-AllowedProperties -Object $entry -AllowedProperties @('id', 'tags') -Scope $entryScope
                $entryId = $entry['id']
                $entryTags = Validate-Tags -TagsValue $entry['tags'] -Scope $entryScope
            } else {
                Add-ValidationError -Scope $entryScope -Message "Entry must be a string id or an object with id/tags."
                continue
            }

            if (-not (Test-NonEmptyString -Value $entryId)) {
                Add-ValidationError -Scope $entryScope -Message "id must be a non-empty string."
                continue
            }

            $trimmedId = $entryId.Trim()
            if (-not (Test-IdFormat -Id $trimmedId)) {
                Add-ValidationError -Scope $entryScope -Message ("id '{0}' must match ^[a-z0-9-]+$." -f $trimmedId)
            }

            if (-not $indexIds.Add($trimmedId)) {
                Add-ValidationError -Scope $entryScope -Message ("Duplicate id '{0}' in index.json." -f $trimmedId)
                continue
            }

            $indexEntries.Add(@{
                    id   = $trimmedId
                    tags = $entryTags
                })
        }
    }
}

$script:stats.IndexEntriesValidated = $indexEntries.Count

$extensionDirectories = @()
if (Test-Path -Path $extensionsRoot -PathType Container) {
    $extensionDirectories = @(Get-ChildItem -Path $extensionsRoot -Directory)
}
$script:stats.ExtensionDirectoriesFound = $extensionDirectories.Count

for ($i = 0; $i -lt $extensionDirectories.Count; $i++) {
    $directoryName = $extensionDirectories[$i].Name
    if (-not $indexIds.Contains($directoryName)) {
        Add-ValidationWarning -Scope ("extensions/{0}" -f $directoryName) -Message "Directory exists but is not referenced in index.json."
    }
}

for ($i = 0; $i -lt $indexEntries.Count; $i++) {
    $entry = $indexEntries[$i]
    $extensionId = [string]$entry['id']
    $extensionDirectory = Join-Path -Path $extensionsRoot -ChildPath $extensionId
    $manifestPath = Join-Path -Path $extensionDirectory -ChildPath 'manifest.json'

    if (-not (Test-Path -Path $extensionDirectory -PathType Container)) {
        Add-ValidationError -Scope 'index.json' -Message ("Entry '{0}' has no matching directory under extensions/." -f $extensionId)
        continue
    }

    $manifest = Get-JsonDocument -Path $manifestPath -Scope (Get-DisplayPath -Path $manifestPath)
    if ($null -eq $manifest) {
        continue
    }

    Validate-Manifest -Manifest $manifest -ManifestPath $manifestPath -ExpectedId $extensionId -ExtensionDirectory $extensionDirectory
    $script:stats.ManifestsValidated++

    $manifestFiles = @(Get-ChildItem -Path $extensionDirectory -File -Filter 'manifest*.json')
    for ($j = 0; $j -lt $manifestFiles.Count; $j++) {
        $file = $manifestFiles[$j]
        if ($file.Name -ieq 'manifest.json') {
            continue
        }

        if ($file.Name -notmatch '^manifest\.[a-zA-Z]{2,3}(-[a-zA-Z0-9]+)*\.json$') {
            Add-ValidationWarning -Scope (Get-DisplayPath -Path $file.FullName) -Message "Unexpected localized manifest naming. Expected manifest.<locale>.json."
            continue
        }

        $localizedManifest = Get-JsonDocument -Path $file.FullName -Scope (Get-DisplayPath -Path $file.FullName)
        if ($null -eq $localizedManifest) {
            continue
        }

        Validate-Manifest -Manifest $localizedManifest -ManifestPath $file.FullName -ExpectedId $extensionId -ExtensionDirectory $extensionDirectory
        $script:stats.LocalizedManifestsValidated++
    }
}

Write-IssuesBySeverity -Severity 'ERROR' -Title 'Errors' -Color Red
Write-IssuesBySeverity -Severity 'WARN' -Title 'Warnings' -Color Yellow
Write-ValidationSummary

$errorCount = @($script:validationIssues | Where-Object { $_.Severity -eq 'ERROR' }).Count
$warningCount = @($script:validationIssues | Where-Object { $_.Severity -eq 'WARN' }).Count

if ($errorCount -gt 0) {
    exit 1
}

if ($TreatWarningsAsErrors -and $warningCount -gt 0) {
    exit 1
}

exit 0
