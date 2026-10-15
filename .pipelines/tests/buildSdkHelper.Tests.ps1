# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$scriptPath = Join-Path $PSScriptRoot '..\..\src\modules\cmdpal\extensionsdk\nuget\BuildSDKHelper.ps1'
$parseErrors = $null
$tokens = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) {
    throw "Could not parse BuildSDKHelper.ps1: $parseErrors"
}

function Get-SdkPhase {
    param([string]$Phase)

    $statement = @($ast.EndBlock.Statements | Where-Object {
        $_ -is [System.Management.Automation.Language.IfStatementAst] -and
        $_.Clauses[0].Item1.Extent.Text -match ('\$BuildStep -ieq "' + $Phase + '"')
    })
    if ($statement.Count -ne 1) {
        throw "Could not identify SDK $Phase phase."
    }
    return [scriptblock]::Create(@'
param($ExpectedDotNetSdkVersion = '', $OutputPath)
$ErrorActionPreference = 'Stop'
$msbuildPath = 'Invoke-SdkTestCommand'
$nugetPath = 'Invoke-SdkTestCommand'
$toolkitProject = 'Toolkit.csproj'
$solutionPath = 'PowerToys.slnx'
$nuspecPath = 'Toolkit.nuspec'
$packageOutputPath = $OutputPath
$Configuration = 'Release'
$VersionOfSDK = '0.13.1'
$IsAzurePipelineBuild = $true
$buildPlatforms = @('x64', 'arm64')
$BuildStep = 'all'
'@ + "`n" + $statement[0].Extent.Text)
}

function Invoke-SdkTestCommand {
    $script:Calls.Add(($args -join ' '))
    $phase = if ($args -contains '-getProperty:NETCoreSdkVersion') {
        'sdk'
    } elseif ($args -contains '/t:Restore') {
        'restore'
    } elseif ($args -contains '/p:Platform=x64') {
        'x64'
    } elseif ($args -contains '/p:Platform=arm64') {
        'arm64'
    } elseif ($args -contains 'pack') {
        'pack'
    } else {
        throw "Unexpected command: $args"
    }
    $global:LASTEXITCODE = if ($phase -eq $script:FailedPhase) { 7 } else { 0 }
    if ($phase -eq 'sdk') {
        $script:ResolvedSdk
    }
}

Describe 'Command Palette SDK build orchestration' {
    BeforeEach {
        $script:Calls = [System.Collections.Generic.List[string]]::new()
        $script:FailedPhase = ''
        $script:ResolvedSdk = '10.0.303'
        $script:SavedExitCode = $global:LASTEXITCODE
    }

    AfterEach {
        $global:LASTEXITCODE = $script:SavedExitCode
    }

    It 'restores and builds both architectures when the resolved SDK matches' {
        & (Get-SdkPhase 'build') -ExpectedDotNetSdkVersion '10.0.303'

        $script:Calls.Count | Should Be 4
        $script:Calls[1] | Should Match '/t:Restore'
        $script:Calls[2] | Should Match '/p:Platform=x64'
        $script:Calls[3] | Should Match '/p:Platform=arm64'
        $script:Calls[3] | Should Match '/p:CIBuild=true'
    }

    It 'rejects an unexpected SDK before restoring' {
        $script:ResolvedSdk = '10.0.113'

        { & (Get-SdkPhase 'build') -ExpectedDotNetSdkVersion '10.0.303' } |
            Should Throw 'Expected .NET SDK'
        $script:Calls.Count | Should Be 1
    }

    It 'stops immediately when SDK resolution fails' {
        $script:FailedPhase = 'sdk'

        { & (Get-SdkPhase 'build') -ExpectedDotNetSdkVersion '10.0.303' } |
            Should Throw 'Failed to resolve'
        $script:Calls.Count | Should Be 1
    }

    It 'does not compile either architecture after restore fails' {
        $script:FailedPhase = 'restore'

        { & (Get-SdkPhase 'build') -ExpectedDotNetSdkVersion '10.0.303' } |
            Should Throw 'Solution restore failed'
        $script:Calls.Count | Should Be 2
    }

    It 'does not continue after the <Phase> architecture build fails' -TestCases @(
        @{ Phase = 'x64'; ExpectedCalls = 3 },
        @{ Phase = 'arm64'; ExpectedCalls = 4 }
    ) {
        param($Phase, $ExpectedCalls)
        $script:FailedPhase = $Phase

        { & (Get-SdkPhase 'build') -ExpectedDotNetSdkVersion '10.0.303' } |
            Should Throw "Toolkit $Phase Release build failed"
        $script:Calls.Count | Should Be $ExpectedCalls
    }

    It 'allows local SDK selection when no expected version is supplied' {
        & (Get-SdkPhase 'build')

        $script:Calls.Count | Should Be 3
        $script:Calls[0] | Should Match '/t:Restore'
    }

    It 'reports a packaging failure instead of continuing successfully' {
        $script:FailedPhase = 'pack'

        { & (Get-SdkPhase 'pack') -OutputPath (Join-Path $TestDrive 'packages') | Out-Null } |
            Should Throw 'SDK packaging failed'
        $script:Calls.Count | Should Be 1
    }
}
