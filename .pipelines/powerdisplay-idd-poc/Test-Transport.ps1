# Copyright (c) Microsoft Corporation
# Licensed under the MIT license. See LICENSE in the project root.

#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$ProbePath,
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][string]$ProductionProbePath
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (Test-Path (Join-Path $ResultsDirectory 'status.json')) { throw 'Use a fresh results directory; refusing stale success evidence.' }
New-Item $ResultsDirectory -ItemType Directory -Force | Out-Null
$result = [ordered]@{
    Passed = $false
    NativeTransportPassed = $false
    NativeHandlesNonzero = $null
    ProductionDiscoveryPassed = $null
    Error = $null
    SessionId = (Get-Process -Id $PID).SessionId
}
try {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try {
        $adminSid = [Security.Principal.SecurityIdentifier]::new(
            [Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
        if ($identity.Groups.Contains($adminSid)) { throw 'Transport must run as a true standard user, not an administrator.' }
    }
    finally { $identity.Dispose() }
    if ($result.SessionId -eq 0 -or -not @(Get-Process explorer | Where-Object SessionId -EQ $result.SessionId).Count) {
        throw 'Transport requires the interactive console desktop.'
    }
    & $ProbePath --self-test 1> (Join-Path $ResultsDirectory 'protocol-tests.json') 2> (Join-Path $ResultsDirectory 'protocol-error.txt')
    if ($LASTEXITCODE) { throw 'DDC/CI emulator protocol self-tests failed.' }
    & $ProbePath --snapshot 1> (Join-Path $ResultsDirectory 'before.json') 2> (Join-Path $ResultsDirectory 'before-error.txt')
    if ($LASTEXITCODE) { throw 'Standard-user snapshot failed.' }
    & $ProbePath --diagnose 1> (Join-Path $ResultsDirectory 'diagnostics.json') 2> (Join-Path $ResultsDirectory 'diagnostics-error.txt')
    $result.DiagnosticExit = $LASTEXITCODE
    if ($result.DiagnosticExit) { throw 'Native diagnostic reads failed.' }
    & $ProbePath --probe 1> (Join-Path $ResultsDirectory 'transport.json') 2> (Join-Path $ResultsDirectory 'transport-error.txt')
    if ($LASTEXITCODE) { throw "Native transport probe failed: exit $LASTEXITCODE" }
    $transport = Get-Content (Join-Path $ResultsDirectory 'transport.json') -Raw | ConvertFrom-Json
    if (-not $transport.Passed) { throw 'Missing assertion-bearing transport success.' }
    $result.NativeTransportPassed = $true
    $diagnostics = Get-Content (Join-Path $ResultsDirectory 'diagnostics.json') -Raw | ConvertFrom-Json
    $result.NativeHandlesNonzero = $diagnostics.NativeHandlesNonzero
    & $ProductionProbePath 1> (Join-Path $ResultsDirectory 'production-discovery.json') 2> (Join-Path $ResultsDirectory 'production-discovery-error.txt')
    $result.ProductionDiscoveryExit = $LASTEXITCODE
    $production = Get-Content (Join-Path $ResultsDirectory 'production-discovery.json') -Raw | ConvertFrom-Json
    if (-not $production.Completed) { throw "Production discovery probe failed: $($production.Error)" }
    $result.ProductionDiscoveryPassed = $production.Passed -and $result.ProductionDiscoveryExit -eq 0
    if (-not $result.ProductionDiscoveryPassed) {
        throw 'BLOCKED: Native DDC/CI works, but production discovery did not retain two brightness-capable fixture monitors.'
    }
    $result.Passed = $true
}
catch { $result.Error = $_.Exception.ToString() }
finally {
    & $ProbePath --snapshot 1> (Join-Path $ResultsDirectory 'after.json') 2> (Join-Path $ResultsDirectory 'after-error.txt')
    if ($LASTEXITCODE) { $result.Passed = $false; $result.Error += ' Final callback snapshot failed.' }
    $result | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $ResultsDirectory 'status.json')
}
if (-not $result.Passed) { throw $result.Error }
