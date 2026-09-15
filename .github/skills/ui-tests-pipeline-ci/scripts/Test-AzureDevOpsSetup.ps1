# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#requires -Version 7.0

<#
.SYNOPSIS
Validates the prompt-free Azure CLI and Azure DevOps capabilities required by the UI-test pipeline skill.

.DESCRIPTION
Performs only read operations and a pipeline preview. A preview expands the selected YAML without
creating a build. The script never initiates sign-in, prints a token, or mutates Azure DevOps.

.PARAMETER Organization
Azure DevOps organization name. Defaults to the internal `microsoft` organization.

.PARAMETER Project
Azure DevOps project name. Defaults to `Dart`.

.PARAMETER PipelineName
Enabled pipeline definition to discover. Defaults to `UI Test Automation`.

.PARAMETER ProbeBranch
Existing full Git ref used for the non-mutating preview, for example `refs/heads/main`.

.PARAMETER ProbeModule
One existing UITest project stem used to expand the preview, without brackets.

.PARAMETER ProbeBuildId
Optional completed build from the discovered pipeline. When omitted, the preflight inspects the ten
newest completed builds and chooses build/test probes automatically.

.EXAMPLE
pwsh .github/skills/ui-tests-pipeline-ci/scripts/Test-AzureDevOpsSetup.ps1

.EXAMPLE
pwsh .github/skills/ui-tests-pipeline-ci/scripts/Test-AzureDevOpsSetup.ps1 `
  -ProbeBranch refs/heads/my-branch `
  -ProbeModule MyModule.UITests
#>

[CmdletBinding()]
param(
    [string] $Organization = 'microsoft',

    [string] $Project = 'Dart',

    [string] $PipelineName = 'UI Test Automation',

    [string] $ProbeBranch = 'refs/heads/main',

    [string] $ProbeModule = 'FancyZones.UITests.Next',

    [long] $ProbeBuildId = 0
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AzureDevOps.ps1')

$checks = [Collections.Generic.List[object]]::new()

function Add-SetupCheck
{
    param(
        [string] $Name,
        [ValidateSet('PASS', 'FAIL', 'SKIP')]
        [string] $Status,
        [bool] $Required,
        [string] $Detail
    )

    $checks.Add([pscustomobject]@{
            Name = $Name
            Status = $Status
            Required = $Required
            Detail = $Detail
        })
}

function Get-FixedError
{
    param(
        [Management.Automation.ErrorRecord] $ErrorRecord,
        [string] $Fallback
    )

    if ($ErrorRecord.Exception -and -not [string]::IsNullOrWhiteSpace($ErrorRecord.Exception.Message))
    {
        return $ErrorRecord.Exception.Message
    }

    return $Fallback
}

$pipelineId = 0
$probeBuild = $null
$probeTestBuild = $null
$buildCandidates = @()
$probeTestRuns = @()

try
{
    if ($PSVersionTable.PSVersion.Major -lt 7)
    {
        throw "PowerShell 7 or newer is required; found $($PSVersionTable.PSVersion)."
    }

    Add-SetupCheck 'PowerShell7' 'PASS' $true $PSVersionTable.PSVersion.ToString()
}
catch
{
    Add-SetupCheck 'PowerShell7' 'FAIL' $true (Get-FixedError $_ 'PowerShell 7 validation failed.')
}

try
{
    $azCommand = Get-Command az -ErrorAction Stop
    $versionOutput = & az version --output json --only-show-errors 2>&1
    if ($LASTEXITCODE -ne 0)
    {
        throw 'Azure CLI version query failed.'
    }

    $versionObject = ($versionOutput | Out-String) | ConvertFrom-Json
    $version = [string]$versionObject.PSObject.Properties['azure-cli'].Value
    if ([string]::IsNullOrWhiteSpace($version))
    {
        throw 'Azure CLI did not report its version.'
    }

    Add-SetupCheck 'AzureCLI' 'PASS' $true "$($azCommand.Source) v$version"
}
catch
{
    Add-SetupCheck 'AzureCLI' 'FAIL' $true (Get-FixedError $_ 'Azure CLI is unavailable.')
}

try
{
    $extensionOutput = & az extension show `
        --name azure-devops `
        --query '{name:name,version:version}' `
        --output json `
        --only-show-errors 2>&1
    if ($LASTEXITCODE -ne 0)
    {
        throw 'The Azure DevOps CLI extension is not installed.'
    }

    $extension = ($extensionOutput | Out-String) | ConvertFrom-Json
    Add-SetupCheck 'AzureDevOpsExtension' 'PASS' $true "$($extension.name) v$($extension.version)"
}
catch
{
    Add-SetupCheck 'AzureDevOpsExtension' 'FAIL' $true (Get-FixedError $_ 'The Azure DevOps CLI extension is unavailable.')
}

try
{
    $session = Test-AzDevOpsSession
    Add-SetupCheck `
        'CachedSignInAndToken' `
        'PASS' `
        $true `
        "tenant=$($session.TenantId); userType=$($session.UserType); expires=$($session.TokenExpiresOn)"
}
catch
{
    Add-SetupCheck 'CachedSignInAndToken' 'FAIL' $true (Get-FixedError $_ 'Azure CLI sign-in validation failed.')
}

try
{
    $projectUri = "https://dev.azure.com/$Organization/_apis/projects/$([Uri]::EscapeDataString($Project))?api-version=7.1"
    $projectInfo = (Invoke-AzDevOpsRest -Uri $projectUri -Organization $Organization -Project $Project).Body
    if (-not $projectInfo.id -or $projectInfo.state -ne 'wellFormed')
    {
        throw "Project '$Project' is unavailable or not ready."
    }

    Add-SetupCheck 'ProjectRead' 'PASS' $true "project=$($projectInfo.name); state=$($projectInfo.state)"
}
catch
{
    Add-SetupCheck 'ProjectRead' 'FAIL' $true (Get-FixedError $_ "Cannot read $Organization/$Project.")
}

try
{
    $encodedPipelineName = [Uri]::EscapeDataString($PipelineName)
    $definitions = @((Invoke-AzDevOpsRest `
                -Uri "_apis/build/definitions?name=$encodedPipelineName&api-version=7.1" `
                -Organization $Organization `
                -Project $Project).Body.value |
            Where-Object queueStatus -EQ 'enabled')
    if ($definitions.Count -ne 1)
    {
        throw "Expected one enabled '$PipelineName' definition; found $($definitions.Count)."
    }

    $pipelineId = [int]$definitions[0].id
    Add-SetupCheck `
        'PipelineDefinitionRead' `
        'PASS' `
        $true `
        "id=$pipelineId; revision=$($definitions[0].revision)"
}
catch
{
    Add-SetupCheck 'PipelineDefinitionRead' 'FAIL' $true (Get-FixedError $_ 'Pipeline discovery failed.')
}

if ($pipelineId -ne 0)
{
    try
    {
        if ($ProbeBuildId -ne 0)
        {
            $probeBuild = (Invoke-AzDevOpsRest `
                    -Uri "_apis/build/builds/${ProbeBuildId}?api-version=7.1" `
                    -Organization $Organization `
                    -Project $Project).Body
            if ([int]$probeBuild.definition.id -ne $pipelineId)
            {
                throw "Probe build $ProbeBuildId does not belong to pipeline $pipelineId."
            }

            if ($probeBuild.status -ne 'completed')
            {
                throw "Probe build $ProbeBuildId is '$($probeBuild.status)'; use a completed build."
            }

            $buildCandidates = @($probeBuild)
        }
        else
        {
            $buildPage = (Invoke-AzDevOpsRest `
                    -Uri "_apis/build/builds?definitions=$pipelineId&statusFilter=completed&queryOrder=queueTimeDescending&%24top=10&api-version=7.1" `
                    -Organization $Organization `
                    -Project $Project).Body
            $buildCandidates = @($buildPage.value)
            if ($buildCandidates.Count -eq 0)
            {
                throw "No completed build was found among the 10 newest runs for pipeline $pipelineId."
            }

            $probeBuild = $buildCandidates[0]
        }

        Add-SetupCheck `
            'BuildRead' `
            'PASS' `
            $true `
            "id=$($probeBuild.id); status=$($probeBuild.status); result=$($probeBuild.result)"
    }
    catch
    {
        Add-SetupCheck 'BuildRead' 'FAIL' $true (Get-FixedError $_ 'Completed build discovery failed.')
    }
}
else
{
    Add-SetupCheck 'BuildRead' 'SKIP' $true 'Pipeline definition was not resolved.'
}

if ($null -ne $probeBuild)
{
    try
    {
        $timeline = (Invoke-AzDevOpsRest `
                -Uri "_apis/build/builds/$($probeBuild.id)/timeline?api-version=7.1" `
                -Organization $Organization `
                -Project $Project).Body
        Add-SetupCheck 'TimelineRead' 'PASS' $true "records=$(@($timeline.records).Count)"
    }
    catch
    {
        Add-SetupCheck 'TimelineRead' 'FAIL' $true (Get-FixedError $_ 'Build timeline read failed.')
    }

    try
    {
        $logs = (Invoke-AzDevOpsRest `
                -Uri "_apis/build/builds/$($probeBuild.id)/logs?api-version=7.1" `
                -Organization $Organization `
                -Project $Project).Body
        Add-SetupCheck 'BuildLogsRead' 'PASS' $true "logs=$(@($logs.value).Count)"
    }
    catch
    {
        Add-SetupCheck 'BuildLogsRead' 'FAIL' $true (Get-FixedError $_ 'Build log read failed.')
    }

    try
    {
        $artifacts = (Invoke-AzDevOpsRest `
                -Uri "_apis/build/builds/$($probeBuild.id)/artifacts?api-version=7.1" `
                -Organization $Organization `
                -Project $Project).Body
        Add-SetupCheck 'ArtifactsRead' 'PASS' $true "artifacts=$(@($artifacts.value).Count)"
    }
    catch
    {
        Add-SetupCheck 'ArtifactsRead' 'FAIL' $true (Get-FixedError $_ 'Pipeline artifact read failed.')
    }

    try
    {
        foreach ($candidate in $buildCandidates)
        {
            $candidateBuildUri = [Uri]::EscapeDataString("vstfs:///Build/Build/$($candidate.id)")
            $candidateRuns = @((Invoke-AzDevOpsRest `
                        -Uri "_apis/test/runs?buildUri=$candidateBuildUri&api-version=7.1" `
                        -Organization $Organization `
                        -Project $Project).Body.value)
            if ($candidateRuns.Count -gt 0)
            {
                $probeTestBuild = $candidate
                $probeTestRuns = $candidateRuns
                break
            }
        }

        if ($probeTestRuns.Count -eq 0)
        {
            throw "No Azure Test run was found for the selected probe build set. Pass -ProbeBuildId with a known test-bearing completed build."
        }

        Add-SetupCheck `
            'TestRunsRead' `
            'PASS' `
            $true `
            "build=$($probeTestBuild.id); runs=$($probeTestRuns.Count)"

        $probeRunId = [long]$probeTestRuns[0].id
        $resultPage = (Invoke-AzDevOpsRest `
                -Uri "_apis/test/Runs/${probeRunId}/results?%24top=1&%24skip=0&api-version=7.1" `
                -Organization $Organization `
                -Project $Project).Body
        $probeResults = @($resultPage.value)
        if ($probeResults.Count -eq 0)
        {
            throw "Probe test run $probeRunId has no results."
        }

        Add-SetupCheck 'TestResultsRead' 'PASS' $true "run=$probeRunId; pageResults=$($probeResults.Count)"

        $probeResultId = [long]$probeResults[0].id
        $attachments = (Invoke-AzDevOpsRest `
                -Uri "_apis/test/Runs/${probeRunId}/Results/${probeResultId}/attachments?api-version=7.1-preview.1" `
                -Organization $Organization `
                -Project $Project).Body
        Add-SetupCheck `
            'TestAttachmentsRead' `
            'PASS' `
            $true `
            "run=$probeRunId; result=$probeResultId; attachments=$(@($attachments.value).Count)"
    }
    catch
    {
        Add-SetupCheck 'AzureTestRead' 'FAIL' $true (Get-FixedError $_ 'Azure Test read failed.')
    }
}
else
{
    Add-SetupCheck 'BuildDependentReads' 'SKIP' $true 'No probe build was resolved.'
}

if ($pipelineId -ne 0)
{
    try
    {
        if (-not $ProbeBranch.StartsWith('refs/heads/', [StringComparison]::Ordinal))
        {
            throw "ProbeBranch must be a full refs/heads/... ref; received '$ProbeBranch'."
        }

        if ([string]::IsNullOrWhiteSpace($ProbeModule))
        {
            throw 'ProbeModule cannot be empty.'
        }

        $previewRequest = @{
            previewRun = $true
            resources = @{ repositories = @{ self = @{ refName = $ProbeBranch } } }
            templateParameters = @{
                buildPlatforms = '- x64'
                enableMsBuildCaching = 'false'
                useVSPreview = 'false'
                useLatestWebView2 = 'false'
                buildSource = 'buildNow'
                specificBuildId = 'xxxx'
                uiTestModules = "[$ProbeModule]"
            }
        }
        $preview = (Invoke-AzDevOpsRest `
                -Uri "_apis/pipelines/${pipelineId}/runs?api-version=7.1-preview.1" `
                -Method Post `
                -Body $previewRequest `
                -Organization $Organization `
                -Project $Project).Body
        if ([long]$preview.id -ne -1 -or [string]::IsNullOrWhiteSpace([string]$preview.finalYaml))
        {
            throw 'Pipeline preview did not return id=-1 and expanded YAML.'
        }

        $stages = @([regex]::Matches($preview.finalYaml, '(?m)^- stage: (.+)$') |
                ForEach-Object { $_.Groups[1].Value.Trim() })
        $expectedStages = @('Build_x64', 'Test_x64Win10_FullBuild', 'Test_x64Win11_FullBuild')
        $missingStages = @($expectedStages | Where-Object { $_ -notin $stages })
        if ($missingStages.Count -ne 0)
        {
            throw "Pipeline preview omitted required stages: $($missingStages -join ', '). Expanded stages: $($stages -join ', ')."
        }

        $moduleAssignments = @($preview.finalYaml -split "`n" |
                Where-Object { $_ -match '^\s*\$modulesRaw\s*=\s*''[^'']*''\s*$' } |
                ForEach-Object { $_.Trim() })
        $expectedModuleAssignment = "`$modulesRaw = '$ProbeModule'"
        if ($moduleAssignments.Count -eq 0)
        {
            throw 'Pipeline preview parser found no literal $modulesRaw assignments; the template quoting or line layout may have changed.'
        }

        if (@($moduleAssignments | Where-Object { $_ -ne $expectedModuleAssignment }).Count -ne 0)
        {
            throw "Pipeline preview did not resolve only module '$ProbeModule'. Assignments: $($moduleAssignments -join ' | ')."
        }

        Add-SetupCheck `
            'PipelinePreview' `
            'PASS' `
            $true `
            "id=$($preview.id); requestedBranch=$ProbeBranch; module=$ProbeModule; stages=$($stages -join ',')"
    }
    catch
    {
        Add-SetupCheck 'PipelinePreview' 'FAIL' $true (Get-FixedError $_ 'Pipeline preview failed.')
    }
}
else
{
    Add-SetupCheck 'PipelinePreview' 'SKIP' $true 'Pipeline definition was not resolved.'
}

try
{
    if ($null -eq $probeBuild)
    {
        throw 'No probe build is available for a repeated read.'
    }

    $secondRead = (Invoke-AzDevOpsRest `
            -Uri "_apis/build/builds/$($probeBuild.id)?api-version=7.1" `
            -Organization $Organization `
            -Project $Project).Body
    Add-SetupCheck 'RepeatedPromptFreeRead' 'PASS' $true "id=$($secondRead.id); prompts=0"
}
catch
{
    Add-SetupCheck 'RepeatedPromptFreeRead' 'FAIL' $true (Get-FixedError $_ 'Repeated prompt-free read failed.')
}

$requiredFailures = @($checks | Where-Object { $_.Required -and $_.Status -ne 'PASS' })
$summary = [pscustomobject]@{
    Ready = $requiredFailures.Count -eq 0
    Organization = $Organization
    Project = $Project
    PipelineName = $PipelineName
    PipelineId = $pipelineId
    ProbeBuildId = if ($probeBuild) { [long]$probeBuild.id } else { $null }
    ProbeTestBuildId = if ($probeTestBuild) { [long]$probeTestBuild.id } else { $null }
    ProbeBranch = $ProbeBranch
    ProbeModule = $ProbeModule
    Checks = $checks.ToArray()
    ProvenWithoutMutation = @(
        'cached Azure CLI sign-in and Azure DevOps token acquisition',
        'project, definition, build, timeline, log, artifact, test-run, result, and attachment reads',
        'Run Pipeline API access and YAML expansion through previewRun=true',
        'repeated prompt-free token-backed REST reads'
    )
    DeliberatelyNotMutated = @(
        'creating an actual pipeline run',
        'canceling a build',
        'retrying or canceling a stage'
    )
    NextStep = if ($requiredFailures.Count -eq 0)
    {
        'Setup is ready. Re-run this preflight after account changes or any 401/403 response.'
    }
    else
    {
        'Setup is not ready. Resolve every required FAIL/SKIP item before queueing or monitoring CI.'
    }
}

$summary | ConvertTo-Json -Depth 8
if (-not $summary.Ready)
{
    exit 1
}