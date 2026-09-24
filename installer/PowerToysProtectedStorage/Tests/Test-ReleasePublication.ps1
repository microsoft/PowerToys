# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
# Native verification process creation is mocked ONLY in this test's process
# scope. No production verification bypass, certificate, or trust store change.
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$verify = (Resolve-Path "$PSScriptRoot\..\Verify-Release.ps1").Path
$source = "$repo\$Platform\$Configuration\ProtectedStorage\PowerToys.ProtectedStorageSetup.exe"
$scratch = Join-Path $PSScriptRoot ("Publication-" + [guid]::NewGuid().ToString('N'))
. "$PSScriptRoot\..\ReleaseTools.ps1"
$testState = @{ ExitCode = 0; Operations = [Collections.Generic.List[string]]::new() }
Set-Item Function:\Start-Process -Value {
    param([string]$FilePath, [string[]]$ArgumentList, [switch]$NoNewWindow, [switch]$Wait, [switch]$PassThru)
    if ($FilePath -cne "$scratch\TrustVerifier.exe" -or $ArgumentList[0] -notin @('verify-file', 'verify-detached') -or
        !$NoNewWindow -or !$Wait -or !$PassThru) { throw 'Unexpected process invocation in publication fixture.' }
    $testState.Operations.Add($ArgumentList[0])
    [pscustomobject]@{ ExitCode = $testState.ExitCode }
}.GetNewClosure()
function Expect-Rejection([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action } catch {
        if ($_.Exception.Message -notlike $Message) { throw }
        $rejected = $true
    }
    if (!$rejected) { throw "Publication accepted invalid input: $Message" }
}
function Write-Ascii([string]$Path, [string]$Value) { [IO.File]::WriteAllText($Path, $Value, [Text.Encoding]::ASCII) }
try {
    $stage = (New-Item -ItemType Directory -Path "$scratch\Stage").FullName
    $publishedDirectory = New-Item -ItemType Directory -Path "$scratch\BinDir"
    $setup = Join-Path $stage 'PowerToys.ProtectedStorageSetup.exe'
    $published = Join-Path $publishedDirectory.FullName 'PowerToys.ProtectedStorageSetup.exe'
    Copy-Item -LiteralPath $source -Destination $setup
    Copy-Item -LiteralPath $source -Destination $published
    foreach ($name in @('Bootstrap.exe', 'Runtime.exe', 'PowerToys.ProtectedStorageMsiAction.exe',
        'PowerToys.ProtectedStorageProvisionBroker.exe', 'PowerToys.ProtectedStorageLifecycle.exe')) {
        Copy-Item -LiteralPath $source -Destination "$stage\$name"
    }
    Write-Ascii "$scratch\TrustVerifier.exe" 'non-executable-process-mock-fixture'
    $carrier = Join-Path $stage 'PowerToys.ProtectedStorage.Carrier.msi'
    Write-Ascii $carrier 'non-installable-publication-fixture'
    $version = ([Diagnostics.FileVersionInfo]::GetVersionInfo($source).FileVersion.Split('.')[0..2]) -join '.'
    $metadata = @{
        version = $version
        peVersion = "$version.0"
        platform = $Platform
        trustPolicy = 'microsoft-production-v1'
        setupSha256 = Get-ReleaseHash $setup
        carrierSha256 = Get-ReleaseHash $carrier
        lifecycleSha256 = Get-ReleaseHash "$stage\PowerToys.ProtectedStorageLifecycle.exe"
    }
    Write-Ascii "$stage\release.json" ($metadata | ConvertTo-Json)
    $clients = @(
        @{ image = 'PowerToys.WorkspacesEditor.exe'; role = 'workspaces.writer'; sha256 = 'a' * 64 },
        @{ image = 'PowerToys.WorkspacesEditor.exe'; role = 'workspaces.preview'; sha256 = 'a' * 64 },
        @{ image = 'PowerToys.WorkspacesLauncher.exe'; role = 'workspaces.launcher'; sha256 = 'b' * 64 },
        @{ image = 'PowerToys.WorkspacesLauncher.exe'; role = 'workspaces.preview'; sha256 = 'b' * 64 },
        @{ image = 'PowerToys.WorkspacesSnapshotTool.exe'; role = 'workspaces.preview'; sha256 = 'c' * 64 },
        @{ image = 'PowerToys.WorkspacesWindowArranger.exe'; role = 'workspaces.arranger'; sha256 = 'd' * 64 },
        @{ image = 'Microsoft.CmdPal.Ext.PowerToys.exe'; role = 'workspaces.reader'; sha256 = 'e' * 64 },
        @{ image = 'PowerToys.ProtectedStorageMsiAction.exe'; role = 'maintenance'; sha256 = Get-ReleaseHash "$stage\PowerToys.ProtectedStorageMsiAction.exe" }
    )
    Write-Ascii "$stage\ClientCatalog.json" (@{ format = 1; app = 'PowerToysProtectedStorage'; version = "$version.0"; clients = $clients } | ConvertTo-Json -Depth 5 -Compress)
    Write-Ascii "$stage\ClientCatalog.p7s" 'non-CMS-native-verification-mocked'
    Write-Ascii "$stage\manifest.p7s" 'non-CMS-native-verification-mocked'
    $manifest = "format=1`napp=PowerToysProtectedStorage`nversion=$version.0`nbootstrap_sha256=$(Get-ReleaseHash "$stage\Bootstrap.exe")`nruntime_sha256=$(Get-ReleaseHash "$stage\Runtime.exe")`ncatalog_sha256=$(Get-ReleaseHash "$stage\ClientCatalog.json")`n"
    $policy = "format=2`napp=PowerToysProtectedStorage`nsigner_policy=microsoft-production-v1`nminimum_version=$version.0`n"
    Write-Ascii "$stage\manifest.txt" $manifest
    Write-Ascii "$stage\policy.txt" $policy
    $parameters = @{ PackageRoot = $stage; TrustPolicy = 'microsoft-production-v1'; TrustVerifierPath = "$scratch\TrustVerifier.exe"; Version = $version; Platform = $Platform; PublishedSetupPath = $published }
    & $verify @parameters
    if (@($testState.Operations | Where-Object { $_ -eq 'verify-file' }).Count -ne 7 -or
        @($testState.Operations | Where-Object { $_ -eq 'verify-detached' }).Count -ne 2) {
        throw 'Publication did not independently verify every helper, MSI, and document.'
    }
    [IO.File]::AppendAllText($published, 'tamper')
    Expect-Rejection { & $verify @parameters } 'The public BinDir Setup*'
    Copy-Item -LiteralPath $source -Destination $published -Force
    [IO.File]::AppendAllText("$stage\Bootstrap.exe", 'tamper')
    Expect-Rejection { & $verify @parameters } 'Signed manifest does not describe*'
    Copy-Item -LiteralPath $source -Destination "$stage\Bootstrap.exe" -Force
    Write-Ascii "$stage\manifest.txt" ($manifest -replace "`n", "`r`n")
    Expect-Rejection { & $verify @parameters } 'Signed manifest does not describe*'
    Write-Ascii "$stage\manifest.txt" $manifest
    [IO.File]::WriteAllText("$stage\policy.txt", $policy, [Text.UTF8Encoding]::new($true))
    Expect-Rejection { & $verify @parameters } 'Seed policy does not exactly match*'
    Write-Ascii "$stage\policy.txt" $policy
    $metadata.trustPolicy = 'development'
    Write-Ascii "$stage\release.json" ($metadata | ConvertTo-Json)
    Expect-Rejection { & $verify @parameters } 'Protected-storage release identity*'
    $metadata.trustPolicy = 'microsoft-production-v1'
    Write-Ascii "$stage\release.json" ($metadata | ConvertTo-Json)
    $testState.ExitCode = 1
    Expect-Rejection { & $verify @parameters } 'Production release trust verification failed*'
    $testState.ExitCode = 0
    Write-Ascii $setup 'non-PE-invalid-Setup-identity'
    $metadata.setupSha256 = Get-ReleaseHash $setup
    Write-Ascii "$stage\release.json" ($metadata | ConvertTo-Json)
    Expect-Rejection { & $verify @parameters } 'Protected-storage Setup has an unexpected version-resource identity*'
    Write-Host 'PASS process-scoped mocked publication: all native gates, exact final hashes/bytes, Setup identity, fixed policy/version, native failure, and tamper/BOM/line-ending rejection.'
} finally {
    Remove-Item Function:\Start-Process
    if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force }
}
