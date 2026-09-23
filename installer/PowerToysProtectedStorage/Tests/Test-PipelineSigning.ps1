# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$templates = "$repo\.pipelines\v2\templates"
$detached = Get-Content "$templates\steps-esrp-sign-files-detached.yml" -Raw
$inline = [regex]::Match($detached, '(?s)inlineOperation: \|-\s*(\[.*\])\s*$')
if (!$inline.Success) { throw 'Missing detached ESRP operation.' }
$operations = @($inline.Groups[1].Value | ConvertFrom-Json)
if ($operations.Count -ne 1 -or $operations[0].KeyCode -cne 'CP-230012' -or
    $operations[0].OperationCode -cne 'Pkcs7DetachedSign' -or
    $operations[0].Parameters.P7CE -cne '/p7ce DetachedSignedData' -or
    $operations[0].Parameters.P7EKU -cne '1.2.840.113549.1.7.1' -or
    $operations[0].Parameters.FileDigest -cne '/fd SHA256') {
    throw 'Detached signing must use the documented ESRP contract without invented SigntoolSign parameters.'
}
$pipeline = Get-Content "$templates\steps-build-protected-storage.yml" -Raw
$previous = -1
foreach ($marker in @(
    'New-ProtectedStorageClientInventory.ps1',
    '-ReleaseStage Payloads',
    'displayName: Sign Protected Storage payloads and MSI action',
    '-ReleaseStage Documents',
    'Copy-Item -LiteralPath "$package\manifest.txt" -Destination "$package\manifest.p7s"',
    'Copy-Item -LiteralPath "$package\ClientCatalog.json" -Destination "$package\ClientCatalog.p7s"',
    'template: steps-esrp-sign-files-detached.yml',
    "split('Lifecycle,Carrier,Broker,Setup', ',')",
    '-ReleaseStage Publish'
)) {
    $position = $pipeline.IndexOf($marker, [StringComparison]::Ordinal)
    if ($position -le $previous) { throw "Missing or out-of-order release phase: $marker" }
    $previous = $position
}
if ($pipeline -match 'SigningCertificateThumbprint|Set-AuthenticodeSignature|New-SelfSignedCertificate') {
    throw 'The remote pipeline must not use a local product-signing key.'
}
$installer = Get-Content "$templates\steps-build-installer-vnext.yml" -Raw
if ($installer.IndexOf('template: steps-build-protected-storage.yml') -gt $installer.IndexOf('Build VNext MSI') -or
    ([regex]::Matches($installer, '/p:ProtectedStorageExpectedSignerSha256=\$\(ProtectedStorageExpectedSignerSha256\)')).Count -ne 4) {
    throw 'Both MSI and both bundle scopes must consume the finalized release and explicit signer pin.'
}
$job = Get-Content "$templates\job-build-project.yml" -Raw
if ($job.IndexOf('displayName: Sign Core PowerToys') -gt $job.IndexOf('template: steps-build-installer-vnext.yml')) {
    throw 'Client signing must precede carrier catalog generation.'
}
$core = Get-Content "$repo\.pipelines\ESRPSigning_core.json" -Raw | ConvertFrom-Json
$signedPaths = @($core.SignBatches | ForEach-Object MatchedPath)
foreach ($path in @('PowerToys.ProtectedStorage.Client.Managed.dll', 'WinUI3Apps\PowerToys.ProtectedStorage.Client.Managed.dll')) {
    if ($path -notin $signedPaths) { throw "Missing managed client signing entry: $path" }
}
Write-Host 'PASS remote signing graph, detached replacement contract, client DLL signing, and both installer scopes.'
