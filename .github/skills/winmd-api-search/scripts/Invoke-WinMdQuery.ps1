<#
.SYNOPSIS
    Query WinMD API metadata from cached JSON files.

.DESCRIPTION
    Reads pre-built JSON cache of WinMD types, members, and namespaces.
    The cache is organized per-package (deduplicated) with project manifests
    that map each project to its referenced packages.

    Supports listing namespaces, types, members, searching, enum value lookup,
    and listing cached projects/packages.

.PARAMETER Action
    The query action to perform:
    - projects    : List cached projects
    - packages    : List packages for a project
    - stats       : Show aggregate statistics for a project
    - namespaces  : List all namespaces (optional -Filter prefix)
    - types       : List types in a namespace (-Namespace required)
    - members     : List members of a type (-TypeName required)
    - search      : Search types and members (-Query required)
    - enums       : List enum values (-TypeName required)

.PARAMETER Project
    Project name to query. Auto-selected if only one project is cached.
    Use -Action projects to list available projects.

.PARAMETER Namespace
    Namespace to query types from (used with -Action types).

.PARAMETER TypeName
    Full type name e.g. "Microsoft.UI.Xaml.Controls.Button" (used with -Action members, enums).

.PARAMETER Query
    Search query string (used with -Action search).

.PARAMETER Filter
    Optional prefix filter for namespaces (used with -Action namespaces).

.PARAMETER CacheDir
    Path to the winmd-cache directory. Defaults to "Generated Files\winmd-cache"
    relative to the workspace root.

.PARAMETER MaxResults
    Maximum number of results to return for search. Defaults to 30.

.EXAMPLE
    .\Invoke-WinMdQuery.ps1 -Action projects
    .\Invoke-WinMdQuery.ps1 -Action packages -Project BlankWInUI
    .\Invoke-WinMdQuery.ps1 -Action stats -Project BlankWInUI
    .\Invoke-WinMdQuery.ps1 -Action namespaces -Filter "Microsoft.UI"
    .\Invoke-WinMdQuery.ps1 -Action types -Namespace "Microsoft.UI.Xaml.Controls"
    .\Invoke-WinMdQuery.ps1 -Action members -TypeName "Microsoft.UI.Xaml.Controls.Button"
    .\Invoke-WinMdQuery.ps1 -Action search -Query "NavigationView"
    .\Invoke-WinMdQuery.ps1 -Action enums -TypeName "Microsoft.UI.Xaml.Visibility"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('projects', 'packages', 'stats', 'namespaces', 'types', 'members', 'search', 'enums')]
    [string]$Action,

    [string]$Project,
    [string]$Namespace,
    [string]$TypeName,
    [string]$Query,
    [string]$Filter,
    [string]$CacheDir,
    [int]$MaxResults = 30
)

# ─── Resolve cache directory ─────────────────────────────────────────────────

if (-not $CacheDir) {
    # Convention: skill lives at .github/skills/winmd-api-search/scripts/
    # so workspace root is 4 levels up from $PSScriptRoot.
    $scriptDir = $PSScriptRoot
    $root = (Resolve-Path (Join-Path $scriptDir '..\..\..\..')).Path
    $CacheDir = Join-Path $root 'Generated Files\winmd-cache'
}

if (-not (Test-Path $CacheDir)) {
    Write-Error "Cache not found at: $CacheDir`nRun: .\Update-WinMdCache.ps1 (from .github\skills\winmd-api-search\scripts\)"
    exit 1
}

# ─── Project resolution helpers ──────────────────────────────────────────────

function Get-CachedProjects {
    $projectsDir = Join-Path $CacheDir 'projects'
    if (-not (Test-Path $projectsDir)) { return @() }
    Get-ChildItem $projectsDir -Filter '*.json' | ForEach-Object { $_.BaseName }
}

function Resolve-ProjectManifest {
    param([string]$Name)

    $projectsDir = Join-Path $CacheDir 'projects'
    if (-not (Test-Path $projectsDir)) {
        Write-Error "No projects cached. Run Update-WinMdCache.ps1 first."
        exit 1
    }

    if ($Name) {
        $path = Join-Path $projectsDir "$Name.json"
        if (-not (Test-Path $path)) {
            $available = (Get-CachedProjects) -join ', '
            Write-Error "Project '$Name' not found. Available: $available"
            exit 1
        }
        return Get-Content $path | ConvertFrom-Json
    }

    # Auto-select if only one project
    $manifests = Get-ChildItem $projectsDir -Filter '*.json' -ErrorAction SilentlyContinue
    if ($manifests.Count -eq 0) {
        Write-Error "No projects cached. Run Update-WinMdCache.ps1 first."
        exit 1
    }
    if ($manifests.Count -eq 1) {
        return Get-Content $manifests[0].FullName | ConvertFrom-Json
    }

    $available = ($manifests | ForEach-Object { $_.BaseName }) -join ', '
    Write-Error "Multiple projects cached — use -Project to specify. Available: $available"
    exit 1
}

function Get-PackageCacheDirs {
    param($Manifest)
    $dirs = @()
    foreach ($pkg in $Manifest.packages) {
        $dir = Join-Path $CacheDir 'packages' $pkg.id $pkg.version
        if (Test-Path $dir) {
            $dirs += $dir
        }
    }
    return $dirs
}

# ─── Action: projects ────────────────────────────────────────────────────────

function Show-Projects {
    $projects = Get-CachedProjects
    if ($projects.Count -eq 0) {
        Write-Output "No projects cached."
        return
    }
    Write-Output "Cached projects ($($projects.Count)):"
    foreach ($p in $projects) {
        $manifest = Get-Content (Join-Path $CacheDir 'projects' "$p.json") | ConvertFrom-Json
        $pkgCount = $manifest.packages.Count
        Write-Output "  $p ($pkgCount package(s))"
    }
}

# ─── Action: packages ────────────────────────────────────────────────────────

function Show-Packages {
    $manifest = Resolve-ProjectManifest -Name $Project
    Write-Output "Packages for project '$($manifest.projectName)' ($($manifest.packages.Count)):"
    foreach ($pkg in $manifest.packages) {
        $metaPath = Join-Path $CacheDir 'packages' $pkg.id $pkg.version 'meta.json'
        if (Test-Path $metaPath) {
            $meta = Get-Content $metaPath | ConvertFrom-Json
            Write-Output "  $($pkg.id)@$($pkg.version) — $($meta.totalTypes) types, $($meta.totalMembers) members"
        } else {
            Write-Output "  $($pkg.id)@$($pkg.version) — (cache missing)"
        }
    }
}

# ─── Action: stats ───────────────────────────────────────────────────────────

function Show-Stats {
    $manifest = Resolve-ProjectManifest -Name $Project
    $totalTypes = 0
    $totalMembers = 0
    $totalNamespaces = 0
    $totalWinMd = 0

    foreach ($pkg in $manifest.packages) {
        $metaPath = Join-Path $CacheDir 'packages' $pkg.id $pkg.version 'meta.json'
        if (Test-Path $metaPath) {
            $meta = Get-Content $metaPath | ConvertFrom-Json
            $totalTypes += $meta.totalTypes
            $totalMembers += $meta.totalMembers
            $totalNamespaces += $meta.totalNamespaces
            $totalWinMd += $meta.winMdFiles.Count
        }
    }

    Write-Output "WinMD Index Statistics — $($manifest.projectName)"
    Write-Output "======================================"
    Write-Output "  Packages:   $($manifest.packages.Count)"
    Write-Output "  Namespaces: $totalNamespaces (may overlap across packages)"
    Write-Output "  Types:      $totalTypes"
    Write-Output "  Members:    $totalMembers"
    Write-Output "  WinMD files: $totalWinMd"
}

# ─── Action: namespaces ──────────────────────────────────────────────────────

function Get-Namespaces {
    param([string]$Prefix)
    $manifest = Resolve-ProjectManifest -Name $Project
    $dirs = Get-PackageCacheDirs -Manifest $manifest
    $allNs = @()

    foreach ($dir in $dirs) {
        $nsFile = Join-Path $dir 'namespaces.json'
        if (Test-Path $nsFile) {
            $allNs += (Get-Content $nsFile | ConvertFrom-Json)
        }
    }

    $allNs = $allNs | Sort-Object -Unique
    if ($Prefix) {
        $allNs = $allNs | Where-Object { $_ -like "$Prefix*" }
    }
    $allNs | ForEach-Object { Write-Output $_ }
}

# ─── Action: types ───────────────────────────────────────────────────────────

function Get-TypesInNamespace {
    param([string]$Ns)
    if (-not $Ns) {
        Write-Error "-Namespace is required for 'types' action."
        exit 1
    }

    $manifest = Resolve-ProjectManifest -Name $Project
    $dirs = Get-PackageCacheDirs -Manifest $manifest
    $safeFile = $Ns.Replace('.', '_') + '.json'
    $found = $false
    $seen = @{}

    foreach ($dir in $dirs) {
        $filePath = Join-Path $dir "types\$safeFile"
        if (-not (Test-Path $filePath)) { continue }
        $found = $true
        $types = Get-Content $filePath | ConvertFrom-Json
        foreach ($t in $types) {
            if ($seen.ContainsKey($t.fullName)) { continue }
            $seen[$t.fullName] = $true
            Write-Output "$($t.kind) $($t.fullName)$(if ($t.baseType) { " : $($t.baseType)" } else { '' })"
        }
    }

    if (-not $found) {
        Write-Error "Namespace not found: $Ns"
        exit 1
    }
}

# ─── Action: members ─────────────────────────────────────────────────────────

function Get-MembersOfType {
    param([string]$FullName)
    if (-not $FullName) {
        Write-Error "-TypeName is required for 'members' action."
        exit 1
    }

    $ns = $FullName.Substring(0, $FullName.LastIndexOf('.'))
    $safeFile = $ns.Replace('.', '_') + '.json'

    $manifest = Resolve-ProjectManifest -Name $Project
    $dirs = Get-PackageCacheDirs -Manifest $manifest

    foreach ($dir in $dirs) {
        $filePath = Join-Path $dir "types\$safeFile"
        if (-not (Test-Path $filePath)) { continue }

        $types = Get-Content $filePath | ConvertFrom-Json
        $type = $types | Where-Object { $_.fullName -eq $FullName }
        if (-not $type) { continue }

        Write-Output "$($type.kind) $($type.fullName)"
        if ($type.baseType) { Write-Output "  Extends: $($type.baseType)" }
        Write-Output ""
        foreach ($m in $type.members) {
            Write-Output "  [$($m.kind)] $($m.signature)"
        }
        return
    }

    Write-Error "Type not found: $FullName"
    exit 1
}

# ─── Action: search ──────────────────────────────────────────────────────────
# Ranks namespaces by best type-name match score.
# Outputs: ranked namespaces with top matching types and the JSON file path.
# The LLM can then read_file the JSON to inspect all members intelligently.

function Search-WinMd {
    param([string]$SearchQuery, [int]$Max)
    if (-not $SearchQuery) {
        Write-Error "-Query is required for 'search' action."
        exit 1
    }

    $manifest = Resolve-ProjectManifest -Name $Project
    $dirs = Get-PackageCacheDirs -Manifest $manifest

    # Collect: namespace → { bestScore, matchingTypes[], filePath }
    $nsResults = @{}

    foreach ($dir in $dirs) {
        $nsFile = Join-Path $dir 'namespaces.json'
        if (-not (Test-Path $nsFile)) { continue }
        $nsList = Get-Content $nsFile | ConvertFrom-Json

        foreach ($n in $nsList) {
            $safeFile = $n.Replace('.', '_') + '.json'
            $filePath = Join-Path $dir "types\$safeFile"
            if (-not (Test-Path $filePath)) { continue }

            $types = Get-Content $filePath | ConvertFrom-Json
            foreach ($t in $types) {
                $score = Get-MatchScore -Name $t.name -FullName $t.fullName -Query $SearchQuery
                if ($score -le 0) { continue }

                if (-not $nsResults.ContainsKey($n)) {
                    $nsResults[$n] = @{ BestScore = 0; Types = @(); FilePath = $filePath }
                }
                $entry = $nsResults[$n]
                if ($score -gt $entry.BestScore) { $entry.BestScore = $score }
                $entry.Types += "$($t.kind) $($t.fullName) [$score]"
            }
        }
    }

    if ($nsResults.Count -eq 0) {
        Write-Output "No results found for: $SearchQuery"
        return
    }

    $ranked = $nsResults.GetEnumerator() |
        Sort-Object { $_.Value.BestScore } -Descending |
        Select-Object -First $Max

    foreach ($r in $ranked) {
        $ns = $r.Key
        $info = $r.Value
        Write-Output "[$($info.BestScore)] $ns"
        Write-Output "    File: $($info.FilePath)"
        # Show top 5 matching types in this namespace
        $info.Types | Select-Object -First 5 | ForEach-Object { Write-Output "    $_" }
        Write-Output ""
    }
}

# ─── Search scoring ──────────────────────────────────────────────────────────
# Simple ranked scoring on type names. Higher = better.
#   100 = exact name    80 = starts-with    60 = substring
#    50 = PascalCase     40 = multi-keyword   20 = fuzzy subsequence

function Get-MatchScore {
    param([string]$Name, [string]$FullName, [string]$Query)

    $q = $Query.Trim()
    if (-not $q) { return 0 }

    if ($Name -eq $q) { return 100 }
    if ($Name -like "$q*") { return 80 }
    if ($Name -like "*$q*" -or $FullName -like "*$q*") { return 60 }

    $initials = ($Name.ToCharArray() | Where-Object { [char]::IsUpper($_) }) -join ''
    if ($initials.Length -ge 2 -and $initials -like "*$q*") { return 50 }

    $words = $q -split '\s+' | Where-Object { $_.Length -gt 0 }
    if ($words.Count -gt 1) {
        $allFound = $true
        foreach ($w in $words) {
            if ($Name -notlike "*$w*" -and $FullName -notlike "*$w*") {
                $allFound = $false
                break
            }
        }
        if ($allFound) { return 40 }
    }

    if (Test-FuzzySubsequence -Text $Name -Pattern $q) { return 20 }

    return 0
}

function Test-FuzzySubsequence {
    param([string]$Text, [string]$Pattern)
    $ti = 0
    $tLower = $Text.ToLowerInvariant()
    $pLower = $Pattern.ToLowerInvariant()
    foreach ($ch in $pLower.ToCharArray()) {
        $idx = $tLower.IndexOf($ch, $ti)
        if ($idx -lt 0) { return $false }
        $ti = $idx + 1
    }
    return $true
}

# ─── Action: enums ───────────────────────────────────────────────────────────

function Get-EnumValues {
    param([string]$FullName)
    if (-not $FullName) {
        Write-Error "-TypeName is required for 'enums' action."
        exit 1
    }

    $ns = $FullName.Substring(0, $FullName.LastIndexOf('.'))
    $safeFile = $ns.Replace('.', '_') + '.json'

    $manifest = Resolve-ProjectManifest -Name $Project
    $dirs = Get-PackageCacheDirs -Manifest $manifest

    foreach ($dir in $dirs) {
        $filePath = Join-Path $dir "types\$safeFile"
        if (-not (Test-Path $filePath)) { continue }

        $types = Get-Content $filePath | ConvertFrom-Json
        $type = $types | Where-Object { $_.fullName -eq $FullName }
        if (-not $type) { continue }

        if ($type.kind -ne 'Enum') {
            Write-Error "$FullName is not an Enum (kind: $($type.kind))"
            exit 1
        }
        Write-Output "Enum $($type.fullName)"
        if ($type.enumValues) {
            $type.enumValues | ForEach-Object { Write-Output "  $_" }
        } else {
            Write-Output "  (no values)"
        }
        return
    }

    Write-Error "Type not found: $FullName"
    exit 1
}

# ─── Dispatch ─────────────────────────────────────────────────────────────────

switch ($Action) {
    'projects'   { Show-Projects }
    'packages'   { Show-Packages }
    'stats'      { Show-Stats }
    'namespaces' { Get-Namespaces -Prefix $Filter }
    'types'      { Get-TypesInNamespace -Ns $Namespace }
    'members'    { Get-MembersOfType -FullName $TypeName }
    'search'     { Search-WinMd -SearchQuery $Query -Max $MaxResults }
    'enums'      { Get-EnumValues -FullName $TypeName }
}
