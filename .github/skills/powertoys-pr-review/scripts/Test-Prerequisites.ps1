<#
.SYNOPSIS
    Verify the four prerequisites for the powertoys-pr-review skill.
.DESCRIPTION
    Checks: (1) a personal fork of microsoft/PowerToys exists, (2) a local clone
    is present, (3) Visual Studio 2022 build tools are installed. Copilot code
    review (prerequisite 3) can only be confirmed by requesting a review on a real
    fork PR, so it is reported as a manual follow-up.
.EXAMPLE
    ./Test-Prerequisites.ps1
#>
[CmdletBinding()]
param()

$results = [ordered]@{}

# 1. Fork exists
$fork = gh repo list --fork --json nameWithOwner 2>$null | ConvertFrom-Json |
    Where-Object { $_.nameWithOwner -like '*/PowerToys' } | Select-Object -First 1
$results['Fork'] = if ($fork) { "OK ($($fork.nameWithOwner))" } else { "MISSING - run: gh repo fork microsoft/PowerToys --clone=false" }

# 2. Local clone
$clone = @('C:\PowerToys', "$env:USERPROFILE\source\repos\PowerToys", "$env:USERPROFILE\git\PowerToys") |
    Where-Object { Test-Path "$_\.git" } | Select-Object -First 1
$results['Clone'] = if ($clone) { "OK ($clone)" } else { "MISSING - clone microsoft/PowerToys locally" }

# 3. Copilot code review - manual confirmation only
$results['CopilotReview'] = "MANUAL - confirm 'Copilot code review' is enabled on the fork settings; the first review request must return a non-empty requested_reviewers"

# 4. Visual Studio build tools
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsPath = if (Test-Path $vswhere) { & $vswhere -latest -property installationPath 2>$null }
$results['VSBuildTools'] = if ($vsPath) { "OK ($vsPath)" } else { "MISSING - install VS 2022 (Desktop C++ + .NET desktop); see references/prerequisites.md" }

$results.GetEnumerator() | ForEach-Object { "{0,-14} {1}" -f $_.Key, $_.Value }
