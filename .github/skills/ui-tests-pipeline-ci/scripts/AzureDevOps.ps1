# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Provides prompt-free Azure DevOps REST access through an existing Azure CLI sign-in.

.DESCRIPTION
Dot-source this file from PowerShell 7. Each REST call obtains an Azure DevOps access token from the
Azure CLI cache, keeps it only in memory, and clears the token and authorization header afterward.
The helper never initiates an interactive sign-in.

.EXAMPLE
. .\.github\skills\ui-tests-pipeline-ci\scripts\AzureDevOps.ps1
Test-AzDevOpsSession
(Invoke-AzDevOpsRest -Uri '_apis/build/builds/123?api-version=7.1').Body
#>

function Test-AzDevOpsSession
{
    [CmdletBinding()]
    param()

    $accountJson = & az account show `
        --query '{tenantId:tenantId,userType:user.type}' `
        --output json `
        --only-show-errors
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($accountJson | Out-String)))
    {
        throw 'No existing Azure CLI sign-in is available. Ask the user to authenticate outside the agent, then retry.'
    }

    $expiresOn = & az account get-access-token `
        --resource '499b84ac-1321-427f-aa17-267ca6975798' `
        --query expiresOn `
        --output tsv `
        --only-show-errors
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($expiresOn | Out-String)))
    {
        throw 'The existing Azure CLI sign-in cannot acquire an Azure DevOps token.'
    }

    $account = $accountJson | ConvertFrom-Json
    [pscustomobject]@{
        TenantId = $account.tenantId
        UserType = $account.userType
        TokenExpiresOn = ($expiresOn | Out-String).Trim()
    }
}

function Invoke-AzDevOpsRest
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Uri,

        [ValidateSet('Get', 'Post', 'Patch', 'Put', 'Delete')]
        [string] $Method = 'Get',

        [object] $Body,

        [string] $OutFile,

        [string] $Organization = 'shine-oss',

        [string] $Project = 'PowerToys'
    )

    $token = $null
    $headers = $null
    try
    {
        $token = (& az account get-access-token `
                --resource '499b84ac-1321-427f-aa17-267ca6975798' `
                --query accessToken `
                --output tsv `
                --only-show-errors | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token))
        {
            throw 'The existing Azure CLI sign-in cannot acquire an Azure DevOps token.'
        }

        $headers = @{ Authorization = "Bearer $token" }
        $resolvedUri = if ($Uri.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase))
        {
            $Uri
        }
        else
        {
            "https://dev.azure.com/$Organization/$Project/$($Uri.TrimStart('/'))"
        }

        $parameters = @{
            Uri = $resolvedUri
            Method = $Method
            Headers = $headers
            ErrorAction = 'Stop'
            ResponseHeadersVariable = 'responseHeaders'
        }

        if ($PSBoundParameters.ContainsKey('Body'))
        {
            $parameters.ContentType = 'application/json'
            $parameters.Body = if ($Body -is [string])
            {
                $Body
            }
            else
            {
                $Body | ConvertTo-Json -Depth 100 -Compress
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($OutFile))
        {
            $parameters.OutFile = $OutFile
        }

        $responseBody = Invoke-RestMethod @parameters
        [pscustomobject]@{
            Body = $responseBody
            Headers = $responseHeaders
        }
    }
    finally
    {
        if ($headers)
        {
            $headers.Clear()
        }

        $token = $null
        Remove-Variable token, headers -ErrorAction SilentlyContinue
    }
}

function Get-AzDevOpsPagedValues
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Uri,

        [string] $Organization = 'shine-oss',

        [string] $Project = 'PowerToys'
    )

    $items = [Collections.Generic.List[object]]::new()
    $pageCount = 0
    $continuationToken = $null
    do
    {
        $separator = if ($Uri.Contains('?')) { '&' } else { '?' }
        $pageUri = if ([string]::IsNullOrWhiteSpace($continuationToken))
        {
            $Uri
        }
        else
        {
            "$Uri${separator}continuationToken=$([Uri]::EscapeDataString($continuationToken))"
        }

        $response = Invoke-AzDevOpsRest `
            -Uri $pageUri `
            -Organization $Organization `
            -Project $Project
        $pageCount++
        foreach ($item in @($response.Body.value))
        {
            $items.Add($item)
        }

        $continuationHeader = $response.Headers['x-ms-continuationtoken']
        $continuationToken = if ($continuationHeader)
        {
            [string]@($continuationHeader)[0]
        }
        else
        {
            $null
        }
    }
    while (-not [string]::IsNullOrWhiteSpace($continuationToken))

    [pscustomobject]@{
        Items = $items.ToArray()
        PageCount = $pageCount
        ContinuationToken = $continuationToken
    }
}

function Get-AzDevOpsUiTestPreviewStages
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $FinalYaml,

        [Parameter(Mandatory)]
        [string] $ProbeModule
    )

    $stageBlocks = [regex]::Matches($FinalYaml, '(?ms)^- stage: (?<name>[^\r\n]+)\r?\n(?<body>.*?)(?=^- stage: |\z)')
    $stages = @($stageBlocks | ForEach-Object { $_.Groups['name'].Value.Trim() })
    $testStages = @('Test_x64Win10_FullBuild', 'Test_x64Win11_FullBuild')
    $missingStages = @((@('Build_x64') + $testStages) | Where-Object { $_ -cnotin $stages })
    if ($missingStages.Count -ne 0)
    {
        throw "Pipeline preview omitted required stages: $($missingStages -join ', '). Expanded stages: $($stages -join ', ')."
    }

    foreach ($stage in $stageBlocks)
    {
        $name = $stage.Groups['name'].Value.Trim()
        if ($name -cnotin $testStages)
        {
            continue
        }

        $body = $stage.Groups['body'].Value
        $assignments = [regex]::Matches($body, '(?m)^[ \t]+SELECTED_UI_TEST_MODULES:[ \t]*(?<value>[^\r\n]*)')
        if ($assignments.Count -eq 0)
        {
            # Older probe branches pass the module selection as a PowerShell string literal.
            $assignments = [regex]::Matches($body, '(?m)^[ \t]+\$modulesRaw[ \t]*=[ \t]*(?<value>''[^''\r\n]*'')[ \t\r]*$')
        }

        if ($assignments.Count -eq 0)
        {
            throw "Pipeline preview found no literal UI test module selection in '$name'."
        }

        $expectedValues = @($ProbeModule, "'$ProbeModule'", "`"$ProbeModule`"")
        foreach ($assignment in $assignments)
        {
            $value = $assignment.Groups['value'].Value.Trim()
            if ($value -cnotin $expectedValues)
            {
                throw "Pipeline preview did not resolve only module '$ProbeModule' in '$name'. Found: $value"
            }
        }
    }

    return $stages
}

function ConvertTo-AzDevOpsBuildSnapshot
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object] $Build,

        [Parameter(Mandatory)]
        [int] $RequestedId,

        [Parameter(Mandatory)]
        [string] $ExpectedBranch,

        [Parameter(Mandatory)]
        [string] $ExpectedSourceVersion
    )

    $requiredProperties = @('id', 'buildNumber', 'status', 'sourceBranch', 'sourceVersion')
    $missingProperties = @($requiredProperties | Where-Object { $null -eq $Build.PSObject.Properties[$_] })
    if ($missingProperties.Count -gt 0)
    {
        throw "Azure DevOps returned a malformed build response for requested build ${RequestedId}; missing: $($missingProperties -join ', ')."
    }

    if ([int]$Build.id -ne $RequestedId)
    {
        throw "Requested build $RequestedId but Azure DevOps returned build $($Build.id)."
    }

    if ([string]$Build.sourceBranch -cne $ExpectedBranch)
    {
        throw "Build $RequestedId source branch '$($Build.sourceBranch)' does not match '$ExpectedBranch'."
    }

    if ([string]$Build.sourceVersion -ine $ExpectedSourceVersion)
    {
        throw "Build $RequestedId source version '$($Build.sourceVersion)' does not match '$ExpectedSourceVersion'."
    }

    $propertyValue = {
        param([string] $Name)

        $property = $Build.PSObject.Properties[$Name]
        if ($null -ne $property)
        {
            $property.Value
        }
    }

    [pscustomobject]@{
        Id = [int]$Build.id
        BuildNumber = [string]$Build.buildNumber
        Status = [string]$Build.status
        Result = [string](& $propertyValue 'result')
        QueueTime = & $propertyValue 'queueTime'
        StartTime = & $propertyValue 'startTime'
        FinishTime = & $propertyValue 'finishTime'
        LastChangedDate = & $propertyValue 'lastChangedDate'
        WebUrl = [string]$Build._links.web.href
    }
}