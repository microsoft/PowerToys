# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$RestoreOnly,
    [switch]$RunTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Variable -Name RepoRoot -Value $repoRoot -Scope Script
. (Join-Path $PSScriptRoot 'build-common.ps1')
if (!(Ensure-VsDevEnvironment)) { throw 'Visual Studio developer environment could not be initialized.' }

$solution = Join-Path $repoRoot 'ProtectedStorage.Integration.slnf'
RestoreThenBuild $solution "/p:SolutionDir=$repoRoot\" $Platform $Configuration ([bool]$RestoreOnly)
if ($RestoreOnly) { return }

& (Join-Path $PSScriptRoot 'build.ps1') -Path (Join-Path $repoRoot 'installer\PowerToysSetupVNext\SilentFilesInUseBA') `
    -Platform $Platform -Configuration $Configuration -ExtraArgs '/t:Restore', "/p:SolutionDir=$repoRoot\"
if ($LASTEXITCODE) { throw "Bootstrapper extension restore failed with $LASTEXITCODE." }
& (Join-Path $PSScriptRoot 'build.ps1') -Path (Join-Path $repoRoot 'installer\PowerToysSetupVNext\SilentFilesInUseBA') `
    -Platform $Platform -Configuration $Configuration -ExtraArgs "/p:SolutionDir=$repoRoot\"
if ($LASTEXITCODE) { throw "Bootstrapper extension build failed with $LASTEXITCODE." }

& (Join-Path $repoRoot 'installer\PowerToysProtectedStorage\Build-Carrier.ps1') -Platform $Platform -Configuration $Configuration -RunTests:$RunTests
if ($LASTEXITCODE) { throw "Carrier native build/test failed with $LASTEXITCODE." }
if (!$RunTests) { return }
if ($Platform -ne $env:PROCESSOR_ARCHITECTURE.Replace('AMD64', 'x64')) {
    throw "Cannot execute $Platform tests on this host; build completed but test execution is not qualified."
}

$output = Join-Path $repoRoot "$Platform\$Configuration"
$results = Join-Path $repoRoot "TestResults\ProtectedStorage\$Platform\$Configuration"
New-Item -ItemType Directory -Path $results -Force | Out-Null
Push-Location $repoRoot
try {
    & (Join-Path $output 'ProtectedStorage\ProtectedStorage.UnitTests.exe')
    if ($LASTEXITCODE) { throw "Native protected-storage tests failed with $LASTEXITCODE." }
    & (Join-Path $output 'ProtectedStorageTests\ProtectedStorage.Client.Managed.UnitTests.exe') --results-directory $results --report-trx
    if ($LASTEXITCODE) { throw "Managed protected-storage tests failed with $LASTEXITCODE." }
    $vstest = Join-Path $env:VSINSTALLDIR 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe'
    & $vstest (Join-Path $output 'tests\Workspaces\Workspaces.Lib.UnitTests.dll') `
        (Join-Path $output 'tests\UpdatingUnitTests\Updating.UnitTests.dll') `
        "/Platform:$Platform" "/ResultsDirectory:$results" '/Logger:trx;LogFileName=protected-storage-integration.trx'
    if ($LASTEXITCODE) { throw "Workspaces/updater unit tests failed with $LASTEXITCODE." }
}
finally { Pop-Location }

& (Join-Path $repoRoot 'installer\PowerToysProtectedStorage\Tests\Test-Authoring.ps1') -Platform $Platform -Configuration $Configuration -Compile
& (Join-Path $repoRoot 'installer\PowerToysProtectedStorage\Tests\Test-ReleaseTools.ps1')
& (Join-Path $repoRoot 'installer\PowerToysProtectedStorage\Tests\Test-PipelineSigning.ps1')
& (Join-Path $repoRoot 'installer\PowerToysProtectedStorage\Tests\Test-ProjectEvaluation.ps1')
& (Join-Path $repoRoot 'installer\PowerToysProtectedStorage\Tests\Test-ReleasePublication.ps1')
RunMSBuild (Join-Path $repoRoot 'installer\PowerToysSetupVNext\PowerToysInstallerVNext.wixproj') '/t:Restore /p:RestorePackagesConfig=true' $Platform $Configuration
& (Join-Path $PSScriptRoot 'Test-ProtectedStorageMainInstaller.ps1') -Platform $Platform -Configuration $Configuration -Compile
Write-Host 'Protected storage integration build and non-installing tests passed. Signed deployment acceptance remains separate.'
