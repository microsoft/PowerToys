$script:AllowedReviewActions = @('comment', 'request-changes', 'hold', 'close', 'custom')
$script:AllowedItemKinds = @('inline', 'companion')
$script:AllowedSeverities = @('critical', 'high', 'medium', 'low')
$script:ForbiddenPublicPatterns = [ordered]@{
    'fork reference' = '(?i)\bfork(?:\s+PR|\s+repository|\s+repo)?\b'
    'non-upstream PowerToys URL' = '(?i)github\.com/(?!microsoft/PowerToys(?:/|$))[^/\s]+/PowerToys(?:/|$)'
    'worktree reference' = '(?i)\bworktree\b'
    'internal review reference' = '(?i)\binternal[- ]review\b|\breview loop\b|\bconverged\b'
    'private validation reference' = '(?i)\breview validation\b|\bvalidated implementation\b'
    'local validation provenance' = '(?i)\bvalidated locally\b|\bverified locally\b|\blocal build\b|\bbuilds locally\b|\btested in my\b'
    'internal local path' = '(?i)(?:[A-Z]:\\(?:Users\\[^\\\s]+|PowerToys(?:-[^\\\s]+)?)\\|\\\\localhost\\)'
    'PowerShell serialization artifact' = '(?is)@\{.*?\}\.contextComment|System\.Management\.Automation\.PSCustomObject'
    'property interpolation artifact' = '(?i)\.contextComment\b'
}

function Read-JsonFile {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "JSON file not found: $Path"
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Invalid JSON in ${Path}: $($_.Exception.Message)"
    }
}

function Write-JsonFileAtomically {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)]$Value
    )

    $temporaryPath = "$Path.tmp"
    $Value | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $temporaryPath -Encoding utf8
    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

function Get-ReviewDataHash {
    param([Parameter(Mandatory)][string]$Path)

    $text = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path))
    return Get-TextSha256 -Text $text
}

function Get-TextSha256 {
    param([Parameter(Mandatory)][string]$Text)

    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    $hash = [Security.Cryptography.SHA256]::HashData($bytes)
    return [Convert]::ToHexString($hash).ToLowerInvariant()
}

function ConvertTo-ReviewTimestamp {
    param([Parameter(Mandatory)]$Value)

    $timestamp = if ($Value -is [datetimeoffset]) {
        [datetimeoffset]$Value
    }
    elseif ($Value -is [datetime]) {
        [datetimeoffset]([datetime]$Value)
    }
    else {
        [datetimeoffset]::Parse(
            [string]$Value,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::AssumeUniversal
        )
    }

    return $timestamp.ToUniversalTime().ToString('o', [Globalization.CultureInfo]::InvariantCulture)
}

function Get-PublicTextErrors {
    param(
        [AllowNull()][string]$Text,
        [Parameter(Mandatory)][string]$Label
    )

    $errors = [System.Collections.Generic.List[string]]::new()
    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $errors.ToArray()
    }

    foreach ($entry in $script:ForbiddenPublicPatterns.GetEnumerator()) {
        if ($Text -match $entry.Value) {
            $errors.Add("$Label contains a forbidden $($entry.Key).")
        }
    }

    return $errors.ToArray()
}

function Test-SuggestionBody {
    param(
        [AllowNull()][string]$Body,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$Severity
    )

    $errors = [System.Collections.Generic.List[string]]::new()
    if ([string]::IsNullOrWhiteSpace($Body)) {
        $errors.Add("$Label is empty.")
        return $errors.ToArray()
    }

    foreach ($errorMessage in Test-ReviewItemBody -Body $Body -Label $Label -Severity $Severity) {
        $errors.Add($errorMessage)
    }

    $matches = [regex]::Matches($Body, '(?ms)```suggestion[ \t]*\r?\n(.+?)\r?\n```')
    if ($matches.Count -ne 1) {
        $errors.Add("$Label must contain exactly one non-empty suggestion block.")
    }
    elseif ([string]::IsNullOrWhiteSpace($matches[0].Groups[1].Value)) {
        $errors.Add("$Label contains an empty suggestion block.")
    }

    if ($Body -match '(?i)<(?:corrected code|code|use [^>]+|placeholder)[^>]*>') {
        $errors.Add("$Label contains a placeholder.")
    }

    foreach ($errorMessage in Get-PublicTextErrors -Text $Body -Label $Label) {
        $errors.Add($errorMessage)
    }

    return $errors.ToArray()
}

function Test-ReviewItemBody {
    param(
        [AllowNull()][string]$Body,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$Severity
    )

    $errors = [System.Collections.Generic.List[string]]::new()
    if ([string]::IsNullOrWhiteSpace($Body)) {
        $errors.Add("$Label is empty.")
        return $errors.ToArray()
    }

    if ($Body -notmatch '(?m)^###\s+\S') {
        $errors.Add("$Label must start with an author-facing Markdown heading.")
    }

    $severityPattern = '(?im)^\*\*Severity:\*\*\s+`' + [regex]::Escape($Severity) + '`\s*$'
    if ($Body -notmatch $severityPattern) {
        $errors.Add("$Label must contain **Severity:** ``$Severity``.")
    }

    foreach ($errorMessage in Get-PublicTextErrors -Text $Body -Label $Label) {
        $errors.Add($errorMessage)
    }

    return $errors.ToArray()
}

function Get-RightSideHunkMap {
    param([AllowNull()][string]$Patch)

    $map = @{}
    if ([string]::IsNullOrEmpty($Patch)) {
        return $map
    }

    $hunk = -1
    $newLine = 0
    foreach ($patchLine in ($Patch -split "`r?`n")) {
        $header = [regex]::Match($patchLine, '^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@')
        if ($header.Success) {
            $hunk++
            $newLine = [int]$header.Groups[1].Value
            continue
        }

        if ($hunk -lt 0 -or $patchLine.StartsWith('\ No newline')) {
            continue
        }

        if ($patchLine.StartsWith('+') -and -not $patchLine.StartsWith('+++')) {
            $map[$newLine] = $hunk
            $newLine++
        }
        elseif ($patchLine.StartsWith('-') -and -not $patchLine.StartsWith('---')) {
            continue
        }
        else {
            $map[$newLine] = $hunk
            $newLine++
        }
    }

    return $map
}

function Test-RightSideRange {
    param(
        [Parameter(Mandatory)][hashtable]$HunkMap,
        [Parameter(Mandatory)][int]$StartLine,
        [Parameter(Mandatory)][int]$Line
    )

    if ($StartLine -lt 1 -or $Line -lt $StartLine) {
        return $false
    }

    if (-not $HunkMap.ContainsKey($StartLine) -or -not $HunkMap.ContainsKey($Line)) {
        return $false
    }

    return $HunkMap[$StartLine] -eq $HunkMap[$Line]
}

function Invoke-GhGet {
    param([Parameter(Mandatory)][string]$Endpoint)

    $output = & gh api $Endpoint
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub API request failed: $Endpoint"
    }

    if ([string]::IsNullOrWhiteSpace($output)) {
        return $null
    }

    return $output | ConvertFrom-Json
}

function Invoke-GhJsonInput {
    param(
        [Parameter(Mandatory)][string]$Endpoint,
        [Parameter(Mandatory)]$Payload,
        [ValidateSet('POST', 'PATCH', 'PUT', 'DELETE')][string]$Method = 'POST'
    )

    $inputPath = Join-Path ([IO.Path]::GetTempPath()) ("powertoys-pr-review-{0}.json" -f [guid]::NewGuid())
    try {
        $Payload | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $inputPath -Encoding utf8
        $output = & gh api $Endpoint --method $Method --input $inputPath
        if ($LASTEXITCODE -ne 0) {
            throw "GitHub API request failed: $Method $Endpoint"
        }

        if ([string]::IsNullOrWhiteSpace($output)) {
            return $null
        }

        return $output | ConvertFrom-Json
    }
    finally {
        if (Test-Path -LiteralPath $inputPath) {
            Remove-Item -LiteralPath $inputPath
        }
    }
}

function Get-GhPagedItems {
    param([Parameter(Mandatory)][string]$Endpoint)

    $items = [System.Collections.Generic.List[object]]::new()
    for ($page = 1; ; $page++) {
        $separator = if ($Endpoint.Contains('?')) { '&' } else { '?' }
        $batch = @(Invoke-GhGet -Endpoint "$Endpoint${separator}per_page=100&page=$page")
        foreach ($item in $batch) {
            $items.Add($item)
        }

        if ($batch.Count -lt 100) {
            break
        }
    }

    return $items.ToArray()
}

function Get-GitHubReviewState {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][int]$PullRequestNumber,
        [switch]$IncludeFiles
    )

    $pullRequest = Invoke-GhGet -Endpoint "repos/$Repository/pulls/$PullRequestNumber"
    $issueComments = @(Get-GhPagedItems -Endpoint "repos/$Repository/issues/$PullRequestNumber/comments")
    $reviewComments = @(Get-GhPagedItems -Endpoint "repos/$Repository/pulls/$PullRequestNumber/comments")
    $reviews = @(Get-GhPagedItems -Endpoint "repos/$Repository/pulls/$PullRequestNumber/reviews")
    $files = if ($IncludeFiles) {
        @(Get-GhPagedItems -Endpoint "repos/$Repository/pulls/$PullRequestNumber/files")
    }
    else {
        @()
    }

    return [pscustomobject]@{
        headSha = [string]$pullRequest.head.sha
        updatedAt = ConvertTo-ReviewTimestamp -Value $pullRequest.updated_at
        issueCommentCount = $issueComments.Count
        reviewCommentCount = $reviewComments.Count
        reviewCount = $reviews.Count
        files = $files
    }
}

function Test-ReviewDataDocument {
    param(
        [Parameter(Mandatory)]$Document,
        [switch]$AllowIncomplete,
        [switch]$CheckGitHub,
        [hashtable]$LiveData
    )

    $errors = [System.Collections.Generic.List[string]]::new()
    if ($Document.schemaVersion -ne 2) {
        $errors.Add('review-data.json must use schemaVersion 2.')
    }

    if ([string]::IsNullOrWhiteSpace([string]$Document.repository)) {
        $errors.Add('review-data.json must define repository.')
    }
    elseif ([string]$Document.repository -ne 'microsoft/PowerToys') {
        $errors.Add('review-data.json repository must be microsoft/PowerToys.')
    }

    $pullRequests = @($Document.prs)
    if ($pullRequests.Count -eq 0) {
        $errors.Add('review-data.json must contain at least one PR.')
        return $errors.ToArray()
    }

    $duplicateNumbers = @($pullRequests | Group-Object number | Where-Object Count -gt 1)
    foreach ($duplicate in $duplicateNumbers) {
        $errors.Add("PR $($duplicate.Name) appears more than once.")
    }

    foreach ($pullRequest in $pullRequests) {
        $prefix = "PR $($pullRequest.number)"
        if ([int]$pullRequest.number -lt 1) {
            $errors.Add("$prefix must use a positive PR number.")
        }

        $phase = ([string]$pullRequest.phase).ToLowerInvariant()
        $isComplete = $phase -in @('ready', 'held', 'error')
        if (-not $isComplete) {
            if (-not $AllowIncomplete) {
                $errors.Add("$prefix is still in phase '$phase'.")
            }

            continue
        }

        if ($phase -ne 'ready') {
            continue
        }

        if ([string]$pullRequest.headSha -notmatch '^[0-9a-fA-F]{40}$') {
            $errors.Add("$prefix must pin a 40-character headSha.")
        }

        if ($null -eq $pullRequest.snapshot) {
            $errors.Add("$prefix is missing its activity snapshot.")
        }
        else {
            try {
                ConvertTo-ReviewTimestamp -Value $pullRequest.snapshot.updatedAt | Out-Null
            }
            catch {
                $errors.Add("$prefix snapshot has an invalid updatedAt.")
            }
            foreach ($propertyName in @('issueCommentCount', 'reviewCommentCount', 'reviewCount')) {
                if ($null -eq $pullRequest.snapshot.$propertyName -or [int]$pullRequest.snapshot.$propertyName -lt 0) {
                    $errors.Add("$prefix snapshot has an invalid $propertyName.")
                }
            }
        }

        if ($null -eq $pullRequest.publicPayload) {
            $errors.Add("$prefix is missing publicPayload.")
            continue
        }

        foreach ($errorMessage in Get-PublicTextErrors -Text ([string]$pullRequest.publicPayload.contextBody) -Label "$prefix contextBody") {
            $errors.Add($errorMessage)
        }

        $items = @($pullRequest.publicPayload.items)
        foreach ($duplicate in @($items | Group-Object id | Where-Object Count -gt 1)) {
            $errors.Add("$prefix has duplicate item id '$($duplicate.Name)'.")
        }

        foreach ($item in $items) {
            $label = "$prefix item '$($item.id)'"
            $kind = ([string]$item.kind).ToLowerInvariant()
            if ([string]::IsNullOrWhiteSpace([string]$item.id)) {
                $errors.Add("$prefix has an item without an id.")
            }
            elseif ([string]$item.id -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$') {
                $errors.Add("$label id must use 1-80 safe identifier characters.")
            }

            if ($kind -notin $script:AllowedItemKinds) {
                $errors.Add("$label has unsupported kind '$kind'.")
            }

            if (([string]$item.severity).ToLowerInvariant() -notin $script:AllowedSeverities) {
                $errors.Add("$label has invalid severity '$($item.severity)'.")
            }

            if ([string]::IsNullOrWhiteSpace([string]$item.title)) {
                $errors.Add("$label is missing a title.")
            }

            if ($kind -eq 'inline') {
                foreach ($errorMessage in Test-SuggestionBody -Body ([string]$item.body) -Label "$label body" -Severity ([string]$item.severity).ToLowerInvariant()) {
                    $errors.Add($errorMessage)
                }

                if ([string]::IsNullOrWhiteSpace([string]$item.path)) {
                    $errors.Add("$label is missing path.")
                }

                $line = [int]$item.line
                $startLine = if ($null -ne $item.startLine) { [int]$item.startLine } else { $line }
                if ($line -lt 1 -or $startLine -lt 1 -or $startLine -gt $line) {
                    $errors.Add("$label has an invalid line range.")
                }

                if (([string]$item.side).ToUpperInvariant() -ne 'RIGHT') {
                    $errors.Add("$label must target side RIGHT.")
                }
            }
            else {
                foreach ($errorMessage in Test-ReviewItemBody -Body ([string]$item.body) -Label "$label body" -Severity ([string]$item.severity).ToLowerInvariant()) {
                    $errors.Add($errorMessage)
                }

                if ([string]$item.body -match '```suggestion') {
                    $errors.Add("$label is a companion note and cannot contain a suggestion block.")
                }
            }
        }

        if ($CheckGitHub) {
            $live = if ($null -ne $LiveData -and $LiveData.ContainsKey([int]$pullRequest.number)) {
                $LiveData[[int]$pullRequest.number]
            }
            else {
                Get-GitHubReviewState -Repository ([string]$Document.repository) -PullRequestNumber ([int]$pullRequest.number) -IncludeFiles
            }

            if ([string]$live.headSha -ne [string]$pullRequest.headSha) {
                $errors.Add("$prefix head moved from $($pullRequest.headSha) to $($live.headSha).")
                continue
            }

            $patches = @{}
            foreach ($file in @($live.files)) {
                $patches[[string]$file.filename] = Get-RightSideHunkMap -Patch ([string]$file.patch)
            }

            foreach ($item in @($items | Where-Object kind -eq 'inline')) {
                $label = "$prefix item '$($item.id)'"
                if (-not $patches.ContainsKey([string]$item.path)) {
                    $errors.Add("$label path '$($item.path)' is not in the current PR diff.")
                    continue
                }

                $line = [int]$item.line
                $startLine = if ($null -ne $item.startLine) { [int]$item.startLine } else { $line }
                if (-not (Test-RightSideRange -HunkMap $patches[[string]$item.path] -StartLine $startLine -Line $line)) {
                    $errors.Add("$label range $startLine-$line is not inside one current RIGHT-side diff hunk.")
                }
            }
        }
    }

    return $errors.ToArray()
}

function Test-ReviewDecisionDocument {
    param(
        [Parameter(Mandatory)]$Decisions,
        [Parameter(Mandatory)]$ReviewData,
        [Parameter(Mandatory)][string]$ExpectedHash
    )

    $errors = [System.Collections.Generic.List[string]]::new()
    if ($Decisions.schemaVersion -ne 2) {
        $errors.Add('review-decisions.json must use schemaVersion 2.')
    }

    if ([string]$Decisions.reviewDataHash -ne $ExpectedHash) {
        $errors.Add('review-decisions.json does not match the current review-data.json hash.')
    }

    foreach ($duplicate in @($Decisions.prs | Group-Object number | Where-Object Count -gt 1)) {
        $errors.Add("review-decisions.json contains duplicate PR $($duplicate.Name).")
    }

    foreach ($pullRequest in @($ReviewData.prs | Where-Object phase -eq 'ready')) {
        if (@($Decisions.prs | Where-Object number -eq $pullRequest.number).Count -ne 1) {
            $errors.Add("review-decisions.json must contain exactly one decision for ready PR $($pullRequest.number).")
        }
    }

    foreach ($decision in @($Decisions.prs)) {
        $pullRequest = @($ReviewData.prs | Where-Object number -eq $decision.number)
        if ($pullRequest.Count -ne 1) {
            $errors.Add("Decision for PR $($decision.number) does not map to exactly one review-data entry.")
            continue
        }

        $pullRequest = $pullRequest[0]
        $prefix = "PR $($decision.number) decision"
        if (([string]$pullRequest.phase).ToLowerInvariant() -ne 'ready') {
            $errors.Add("$prefix targets a PR that is not ready.")
            continue
        }

        if ([string]$decision.headSha -ne [string]$pullRequest.headSha) {
            $errors.Add("$prefix headSha does not match review-data.json.")
        }

        $action = ([string]$decision.action).ToLowerInvariant()
        if ($action -notin $script:AllowedReviewActions) {
            $errors.Add("$prefix has unsupported action '$action'.")
        }

        if ($action -eq 'approve') {
            $errors.Add("$prefix cannot approve an upstream PR.")
        }

        foreach ($errorMessage in Get-PublicTextErrors -Text ([string]$decision.contextBody) -Label "$prefix contextBody") {
            $errors.Add($errorMessage)
        }

        $knownIds = @($pullRequest.publicPayload.items.id)
        foreach ($property in @($decision.items.PSObject.Properties)) {
            if ($property.Name -notin $knownIds) {
                $errors.Add("$prefix selects unknown item '$($property.Name)'.")
            }

            if ([string]$property.Value -notin @('post', 'hold')) {
                $errors.Add("$prefix item '$($property.Name)' must be post or hold.")
            }
        }

        foreach ($knownId in $knownIds) {
            if ($knownId -notin @($decision.items.PSObject.Properties.Name)) {
                $errors.Add("$prefix is missing item '$knownId'.")
            }
        }

        if ($action -eq 'request-changes') {
            $selectedCompanions = @(
                $pullRequest.publicPayload.items |
                    Where-Object {
                        $_.kind -eq 'companion' -and
                        [string]$decision.items.($_.id) -eq 'post'
                    }
            )
            if (-not $decision.postContext -and $selectedCompanions.Count -eq 0) {
                $errors.Add("$prefix requires a public review body for request-changes.")
            }
        }
    }

    return $errors.ToArray()
}

function Get-ApprovedReviewPlan {
    param(
        [Parameter(Mandatory)]$ReviewData,
        [Parameter(Mandatory)]$Decision
    )

    $pullRequest = @($ReviewData.prs | Where-Object number -eq $Decision.number)[0]
    $selectedIds = @(
        $Decision.items.PSObject.Properties |
            Where-Object { [string]$_.Value -eq 'post' } |
            ForEach-Object Name
    )
    $selectedItems = @($pullRequest.publicPayload.items | Where-Object id -in $selectedIds)
    $inlineComments = @(
        foreach ($item in @($selectedItems | Where-Object kind -eq 'inline')) {
            $comment = [ordered]@{
                path = [string]$item.path
                line = [int]$item.line
                side = 'RIGHT'
                body = [string]$item.body
            }
            if ($null -ne $item.startLine -and [int]$item.startLine -ne [int]$item.line) {
                $comment.start_line = [int]$item.startLine
                $comment.start_side = 'RIGHT'
            }

            [pscustomobject]$comment
        }
    )

    $bodyParts = [System.Collections.Generic.List[string]]::new()
    if ($Decision.postContext -and -not [string]::IsNullOrWhiteSpace([string]$Decision.contextBody)) {
        $bodyParts.Add(([string]$Decision.contextBody).Trim())
    }

    foreach ($item in @($selectedItems | Where-Object kind -eq 'companion')) {
        $bodyParts.Add(([string]$item.body).Trim())
    }

    return [pscustomobject]@{
        number = [int]$Decision.number
        headSha = [string]$pullRequest.headSha
        action = ([string]$Decision.action).ToLowerInvariant()
        event = if (([string]$Decision.action).ToLowerInvariant() -eq 'request-changes') { 'REQUEST_CHANGES' } else { 'COMMENT' }
        body = $bodyParts -join "`n`n---`n`n"
        comments = $inlineComments
        snapshot = $pullRequest.snapshot
    }
}

function Get-ReviewResumeAction {
    param(
        [Parameter(Mandatory)][bool]$BranchExists,
        [bool]$LocalBranchExists,
        [Parameter(Mandatory)][bool]$ReviewPullRequestExists,
        [bool]$ReviewPullRequestOpen = $true,
        [AllowEmptyString()][string]$WorktreePath,
        [int]$CopilotReviewCount,
        [int]$UnresolvedThreadCount,
        [AllowNull()][Nullable[datetime]]$LatestCopilotReviewAt,
        [AllowNull()][Nullable[datetime]]$NewestCommitAt
    )

    if (-not $BranchExists -and -not $ReviewPullRequestExists -and
        ($LocalBranchExists -or -not [string]::IsNullOrWhiteSpace($WorktreePath))) {
        return 'push-and-create-review-pr'
    }
    if (-not $BranchExists -and -not $ReviewPullRequestExists) {
        return 'fresh-mirror'
    }
    if ($BranchExists -and -not $ReviewPullRequestExists) {
        return 'create-review-pr'
    }
    if ($ReviewPullRequestExists -and -not $ReviewPullRequestOpen) {
        return 'reopen-or-create-review-pr'
    }
    if ([string]::IsNullOrWhiteSpace($WorktreePath)) {
        return 'create-worktree'
    }
    if ($CopilotReviewCount -eq 0 -or $UnresolvedThreadCount -gt 0) {
        return 'resume-review-loop'
    }
    if ($null -eq $LatestCopilotReviewAt -or $null -eq $NewestCommitAt -or $NewestCommitAt -gt $LatestCopilotReviewAt) {
        return 'resume-review-loop'
    }

    return 'rebuild-and-draft'
}
