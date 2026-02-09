# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [string]$IndexJson = ".\src\settings-ui\Settings.UI\Assets\Settings\search.index.json",
    [string]$ReswPath = ".\src\settings-ui\Settings.UI\Strings\en-us\Resources.resw",
    [string]$OutputDir = ".\tools\SettingsSearchEvaluation\cases",
    [int]$CasesPerGroup = 200
)

$ErrorActionPreference = "Stop"

$UiArtifactPattern = "(?i)(button|control|header|title|textbox|label|expander|group|page|ui|card|separator|tooltip)"
$FunctionalSignalPattern = "(?i)(enable|toggle|shortcut|hotkey|launch|mode|preview|thumbnail|color|opacity|theme|backup|restore|clipboard|rename|search|language|layout|zone|mouse|keyboard|image|file|power|workspace|zoom|accent|registry|hosts|awake|measure|crop|extract|template|history|monitor|plugin|format|encoding|command|sound|sleep)"
$LowValueIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
[void]$LowValueIds.Add("SearchResults_Title")
[void]$LowValueIds.Add("Activation_Shortcut")
[void]$LowValueIds.Add("Appearance_Behavior")
[void]$LowValueIds.Add("Admin_Mode_Running_As")

function Get-DeterministicIndex {
    param(
        [string]$Text,
        [int]$Modulo
    )

    if ($Modulo -le 0) {
        return 0
    }

    $sum = 0
    foreach ($ch in $Text.ToCharArray()) {
        $sum += [int][char]$ch
    }

    return [Math]::Abs($sum) % $Modulo
}

function Convert-IdToPhrase {
    param([string]$Id)

    if ([string]::IsNullOrWhiteSpace($Id)) {
        return ""
    }

    $text = $Id -replace "_", " "
    $text = [Regex]::Replace($text, "([a-z0-9])([A-Z])", '$1 $2')
    $text = [Regex]::Replace($text, "\s+", " ").Trim()
    return $text.ToLowerInvariant()
}

function Get-ResourceMap {
    param([string]$ReswFile)

    if (-not (Test-Path $ReswFile)) {
        return @{}
    }

    [xml]$xml = Get-Content -Raw $ReswFile
    $map = @{}
    foreach ($data in $xml.root.data) {
        $name = "$($data.name)".Trim()
        $value = "$($data.value)".Trim()
        if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($value)) {
            continue
        }

        $map[$name] = $value
        $map[$name.Replace('/', '.')] = $value
        $map[$name.Replace('.', '/')] = $value
    }

    return $map
}

function Get-ResourceValue {
    param(
        [hashtable]$Map,
        [string[]]$Keys
    )

    foreach ($key in $Keys) {
        if ($Map.ContainsKey($key) -and -not [string]::IsNullOrWhiteSpace($Map[$key])) {
            return "$($Map[$key])".Trim()
        }
    }

    return ""
}

function Get-EntryPhrase {
    param(
        [object]$Entry,
        [hashtable]$ResourceMap
    )

    $id = "$($Entry.elementUid)".Trim()
    if ([string]::IsNullOrWhiteSpace($id)) {
        return ""
    }

    $type = [int]$Entry.type
    $text = ""
    if ($type -eq 0) {
        $text = Get-ResourceValue -Map $ResourceMap -Keys @(
            "$id.ModuleTitle",
            "$id/ModuleTitle"
        )
    }
    else {
        $text = Get-ResourceValue -Map $ResourceMap -Keys @(
            "$id.Header",
            "$id/Header",
            "$id.Content",
            "$id/Content"
        )
    }

    if ([string]::IsNullOrWhiteSpace($text)) {
        $text = Convert-IdToPhrase -Id $id
    }

    $text = [Regex]::Replace($text.ToLowerInvariant(), "\s+", " ").Trim()
    return $text
}

function Get-SemanticSubject {
    param([string]$Phrase)

    if ([string]::IsNullOrWhiteSpace($Phrase)) {
        return ""
    }

    $subject = $Phrase
    $subject = [Regex]::Replace($subject, "\b(settings|setting|card|control|toggle|header|text|button|expander|group|page|ui|title)\b", " ")
    $subject = [Regex]::Replace($subject, "\s+", " ").Trim()
    if ([string]::IsNullOrWhiteSpace($subject)) {
        return $Phrase
    }

    return $subject
}

function Get-FuzzyQuery {
    param(
        [string]$Phrase,
        [string]$Id
    )

    if ([string]::IsNullOrWhiteSpace($Phrase)) {
        return $Id.ToLowerInvariant()
    }

    $words = @($Phrase -split " " | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $mode = Get-DeterministicIndex -Text $Id -Modulo 5

    switch ($mode) {
        0 {
            if ($words.Count -ge 2) {
                return "$($words[0])$($words[1])"
            }

            return ($words -join "")
        }
        1 {
            if ($words.Count -ge 2) {
                return "$($words[0]) $($words[-1])"
            }

            return "$Phrase settings"
        }
        2 {
            $q = $Phrase -replace " and ", " & "
            if ($q -ne $Phrase) {
                return $q
            }

            return ($words -join "")
        }
        3 {
            if ($words.Count -ge 3) {
                return "$($words[0]) $($words[1])"
            }

            return "$Phrase option"
        }
        default {
            $q = $Phrase -replace "power toys", "powertoys"
            if ($q -ne $Phrase) {
                return $q
            }

            return ($words -join "")
        }
    }
}

function Get-TypoQuery {
    param([string]$Phrase)

    if ([string]::IsNullOrWhiteSpace($Phrase)) {
        return "typo"
    }

    $words = @($Phrase -split " " | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($words.Count -eq 0) {
        return $Phrase
    }

    $longestIndex = 0
    for ($i = 1; $i -lt $words.Count; $i++) {
        if ($words[$i].Length -gt $words[$longestIndex].Length) {
            $longestIndex = $i
        }
    }

    $target = $words[$longestIndex]
    if ($target.Length -ge 5) {
        $removeAt = [Math]::Floor($target.Length / 2)
        $target = $target.Remove([int]$removeAt, 1)
    }
    elseif ($target.Length -ge 3) {
        $chars = $target.ToCharArray()
        $tmp = $chars[1]
        $chars[1] = $chars[2]
        $chars[2] = $tmp
        $target = -join $chars
    }
    else {
        $target = "$target$target"
    }

    $words[$longestIndex] = $target
    return ($words -join " ")
}

function Get-SemanticQuery {
    param(
        [string]$Phrase,
        [string]$Id
    )

    $subject = Get-SemanticSubject -Phrase $Phrase
    $idLower = $Id.ToLowerInvariant()
    $action = "configure"

    if ($idLower -match "enable|toggle") {
        $action = "enable"
    }
    elseif ($idLower -match "shortcut|hotkey") {
        $action = "change shortcut for"
    }
    elseif ($idLower -match "launch") {
        $action = "launch"
    }
    elseif ($idLower -match "color|opacity|theme") {
        $action = "change"
    }
    elseif ($idLower -match "preview|thumbnail") {
        $action = "set preview for"
    }
    elseif ($idLower -match "backup|restore") {
        $action = "manage backup for"
    }
    elseif ($idLower -match "rename") {
        $action = "bulk rename with"
    }
    elseif ($idLower -match "awake|sleep") {
        $action = "keep pc awake with"
    }

    $templates = @(
        "how do i {0} {1}",
        "where can i {0} {1}",
        "i want to {0} {1}",
        "help me {0} {1}"
    )
    $templateIndex = Get-DeterministicIndex -Text $Id -Modulo $templates.Count
    return [string]::Format($templates[$templateIndex], $action, $subject)
}

function Select-EntriesForCases {
    param(
        [object[]]$Entries,
        [int]$Count
    )

    $modules = @($Entries | Where-Object { $_.type -eq 0 } | Sort-Object elementUid)
    $others = @($Entries | Where-Object { $_.type -ne 0 })
    $highSignal = @($others | Where-Object { "$($_.elementUid)" -match $FunctionalSignalPattern } | Sort-Object elementUid)
    $remaining = @($others | Where-Object { "$($_.elementUid)" -notmatch $FunctionalSignalPattern } | Sort-Object elementUid)
    $ordered = @($modules + $highSignal + $remaining)
    if ($ordered.Count -lt $Count) {
        throw "Only found $($ordered.Count) candidate entries; need at least $Count."
    }

    return @($ordered | Select-Object -First $Count)
}

function Test-FunctionalCandidate {
    param([object]$Entry)

    $id = "$($Entry.elementUid)".Trim()
    if ([string]::IsNullOrWhiteSpace($id)) {
        return $false
    }

    if ($LowValueIds.Contains($id)) {
        return $false
    }

    if ($Entry.type -eq 0) {
        return $true
    }

    if ($id -match $UiArtifactPattern) {
        return $false
    }

    if ($id -notmatch $FunctionalSignalPattern) {
        return $false
    }

    return $true
}

$resolvedIndex = (Resolve-Path $IndexJson).Path
$entriesRaw = Get-Content -Raw $resolvedIndex | ConvertFrom-Json
$resolvedResw = $null
if (Test-Path $ReswPath) {
    $resolvedResw = (Resolve-Path $ReswPath).Path
}
else {
    Write-Warning "Resource file not found: $ReswPath. Falling back to UID-derived phrases."
}

$resourceMap = if ($resolvedResw) { Get-ResourceMap -ReswFile $resolvedResw } else { @{} }

$byId = @{}
foreach ($entry in $entriesRaw) {
    $id = "$($entry.elementUid)".Trim()
    if ([string]::IsNullOrWhiteSpace($id)) {
        continue
    }

    if (-not $byId.ContainsKey($id)) {
        $byId[$id] = $entry
    }
}

$candidates = @($byId.Values | Where-Object { Test-FunctionalCandidate $_ })
$selected = Select-EntriesForCases -Entries $candidates -Count $CasesPerGroup

$groups = [ordered]@{
    exact = @()
    fuzzy = @()
    typo = @()
    semantic = @()
}

foreach ($entry in $selected) {
    $id = "$($entry.elementUid)".Trim()
    $phrase = Get-EntryPhrase -Entry $entry -ResourceMap $resourceMap
    if ([string]::IsNullOrWhiteSpace($phrase)) {
        $phrase = Convert-IdToPhrase -Id $id
    }

    $groups.exact += [ordered]@{
        group = "exact"
        query = $phrase
        expectedIds = @($id)
        notes = "exact:$id"
    }

    $groups.fuzzy += [ordered]@{
        group = "fuzzy"
        query = (Get-FuzzyQuery -Phrase $phrase -Id $id)
        expectedIds = @($id)
        notes = "fuzzy:$id"
    }

    $groups.typo += [ordered]@{
        group = "typo"
        query = (Get-TypoQuery -Phrase $phrase)
        expectedIds = @($id)
        notes = "typo:$id"
    }

    $groups.semantic += [ordered]@{
        group = "semantic"
        query = (Get-SemanticQuery -Phrase $phrase -Id $id)
        expectedIds = @($id)
        notes = "semantic:$id"
    }
}

if (-not (Test-Path $OutputDir)) {
    New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null
}

foreach ($groupName in $groups.Keys) {
    $out = Join-Path $OutputDir ("settings-search-cases.{0}.200.json" -f $groupName)
    ($groups[$groupName] | ConvertTo-Json -Depth 5) | Set-Content -Path $out -Encoding UTF8
}

$combined = @($groups.exact + $groups.fuzzy + $groups.typo + $groups.semantic)
$combinedPath = Join-Path $OutputDir "settings-search-cases.grouped.800.json"
($combined | ConvertTo-Json -Depth 5) | Set-Content -Path $combinedPath -Encoding UTF8

Write-Host "Generated grouped case files in '$OutputDir'."
Write-Host "  exact:    $($groups.exact.Count)"
Write-Host "  fuzzy:    $($groups.fuzzy.Count)"
Write-Host "  typo:     $($groups.typo.Count)"
Write-Host "  semantic: $($groups.semantic.Count)"
Write-Host "  combined: $($combined.Count)"
Write-Host "Combined file: $combinedPath"
