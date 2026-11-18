# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

function Assert-UiTestProjectStems {
    param([string[]] $Modules)
    foreach ($module in $Modules) {
        if ($module -cnotmatch '\A[A-Za-z0-9][A-Za-z0-9._-]*\z') {
            throw 'Invalid UI-test project stem: use only ASCII letters, digits, dots, underscores or hyphens.'
        }
    }
}

function Assert-UiTestArtifactPattern {
    param(
        [AllowEmptyString()][string] $Pattern,
        [string[]] $Modules = @()
    )
    Assert-UiTestProjectStems $Modules
    $expected = if ($Modules.Count -eq 0) {
        @('**/tests/*UITest*/**')
    } else {
        @($Modules | ForEach-Object { "**/tests/$_/**" })
    }
    $actual = @($Pattern -split '\r?\n' | Where-Object { $_ })
    if ($actual.Count -ne $expected.Count -or
        @(Compare-Object ($expected | Sort-Object) ($actual | Sort-Object)).Count -ne 0) {
        throw "UI-test artifact patterns do not match the requested selection. Expected: $($expected -join '; '); received: '$Pattern'."
    }
}

function Get-UiTestArtifactSelection {
    param(
        [Parameter(Mandatory)][string] $RepoRoot,
        [string[]] $Modules = @(),
        [Parameter(Mandatory)][ValidateSet('x64', 'arm64')][string] $Platform
    )

    $projects = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'src') -Filter '*UITests*.csproj' -File -Recurse -ErrorAction Stop)
    if ($Modules.Count -eq 0) {
        $Modules = @($projects.BaseName | Sort-Object -Unique)
    }
    if ($Modules.Count -eq 0 -or @($Modules | Sort-Object -Unique).Count -ne $Modules.Count) {
        throw 'UI-test artifact selection must contain distinct known projects.'
    }

    foreach ($module in $Modules) {
        $matching = @($projects | Where-Object BaseName -eq $module)
        if ($matching.Count -ne 1) {
            throw "Expected one UI-test project named '$module'; found $($matching.Count)."
        }
        [xml] $project = Get-Content -LiteralPath $matching[0].FullName -Raw -ErrorAction Stop
        $folders = @($project.SelectNodes("//*[local-name()='OutputPath']") | ForEach-Object {
            if ($_.InnerText -match '\\tests\\([^\\]+)\\') { $Matches[1] }
        } | Sort-Object -Unique)
        $assemblies = @($project.SelectNodes("//*[local-name()='AssemblyName']").InnerText | Sort-Object -Unique)
        if ($folders.Count -ne 1 -or $folders[0] -cne $module -or
            ($assemblies.Count -gt 0 -and ($assemblies.Count -ne 1 -or $assemblies[0] -cne $module))) {
            throw "UI-test project '$module' must use its project stem for its tests output folder and assembly."
        }

        # MWB has a separate, source-bound private runtime manifest and validation path.
        if ($module -eq 'MouseWithoutBorders.UITests') {
            [pscustomobject]@{
                Name = $module
                Validation = if ($Platform -eq 'arm64') { 'UnsupportedPlatform' } else { 'MwbRuntimeManifest' }
                Files = @()
            }
            continue
        }

        $packages = @($project.SelectNodes("//*[local-name()='FileWrites']") | ForEach-Object {
            foreach ($match in [regex]::Matches($_.Include, '\$\(TargetDir\)([A-Za-z0-9._-]+\.msix)(?:;|$)')) {
                $match.Groups[1].Value
            }
        } | Sort-Object -Unique)
        [pscustomobject]@{
            Name = $module
            Validation = 'GenericPayload'
            Files = @("$module.exe", "$module.dll", "$module.deps.json", "$module.runtimeconfig.json") + $packages
        }
    }
}

function Assert-UiTestArtifactPayload {
    param(
        [Parameter(Mandatory)][string] $ArtifactRoot,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]] $Selection
    )

    if (-not (Test-Path -LiteralPath $ArtifactRoot -PathType Container)) {
        throw "UI-test artifact root does not exist: $ArtifactRoot"
    }
    foreach ($suite in $Selection) {
        if ($suite.Validation -in @('UnsupportedPlatform', 'MwbRuntimeManifest')) {
            [pscustomobject]@{ Name = $suite.Name; Validation = $suite.Validation; Files = @() }
            continue
        }
        if ($suite.Validation -ne 'GenericPayload') {
            throw "Unknown payload validation route for '$($suite.Name)'."
        }

        $folders = @(Get-ChildItem -LiteralPath $ArtifactRoot -Directory -Recurse -Filter $suite.Name -ErrorAction Stop |
            Where-Object { $_.Parent.Name -eq 'tests' })
        if ($folders.Count -ne 1) {
            throw "Incomplete UI-test payload: expected one tests\$($suite.Name) folder; found $($folders.Count)."
        }
        $runners = @(Get-ChildItem -LiteralPath $folders[0].FullName -File -Recurse `
            -Filter "$($suite.Name).runtimeconfig.json" -ErrorAction Stop)
        if ($runners.Count -ne 1) {
            throw "Incomplete UI-test payload: expected one runner for '$($suite.Name)'; found $($runners.Count)."
        }

        $files = @(foreach ($name in $suite.Files) {
            $path = Join-Path $runners[0].DirectoryName $name
            if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
                throw "Incomplete UI-test payload: required file is missing or empty: $path"
            }
            [pscustomobject]@{
                Path = [IO.Path]::GetRelativePath($ArtifactRoot, $path)
                Length = (Get-Item -LiteralPath $path).Length
                Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256 -ErrorAction Stop).Hash
            }
        })
        [pscustomobject]@{ Name = $suite.Name; Validation = 'GenericPayload'; Files = $files }
    }
}
