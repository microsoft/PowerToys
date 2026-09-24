# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$templates = "$repo\.pipelines\v2\templates"
$detached = Get-Content "$templates\steps-esrp-sign-files-detached.yml" -Raw
$inline = [regex]::Match($detached, '(?s)inlineOperation: \|-\s*(\[.*?\])')
if (!$inline.Success) { throw 'Missing detached ESRP operation.' }
$operations = @($inline.Groups[1].Value | ConvertFrom-Json)
if ($operations.Count -ne 1 -or $operations[0].KeyCode -cne 'CP-230012' -or
    $operations[0].OperationCode -cne 'Pkcs7DetachedSign' -or
    $operations[0].Parameters.P7CE -cne '/p7ce DetachedSignedData' -or
    $operations[0].Parameters.P7EKU -cne '1.2.840.113549.1.7.1' -or
    $operations[0].Parameters.FileDigest -cne '/fd SHA256') {
    throw 'Detached signing must use the documented ESRP contract without invented SigntoolSign parameters.'
}
if (@($operations[0].Parameters.PSObject.Properties).Count -ne 3 -or
    $detached.IndexOf('Add-DetachedTimestamp.ps1') -lt $inline.Index -or
    !$detached.Contains("-TimestampServer 'http://timestamp.acs.microsoft.com'") -or
    !$detached.Contains('-TrustVerifierPath ''${{ parameters.trustVerifierPath }}''')) {
    throw 'RFC3161 must be added after the unchanged ESRP contract and independently production-verified.'
}
$pipeline = Get-Content "$templates\steps-build-protected-storage.yml" -Raw
$previous = -1
foreach ($marker in @(
    '-Path .\src\common\ProtectedStorage\ProtectedStorage.TrustVerifier',
    'displayName: Build x64 host Protected Storage trust verifier',
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
if ($pipeline -match 'SigningCertificateThumbprint|Set-AuthenticodeSignature|New-SelfSignedCertificate|ExpectedSignerSha256|signer_sha256|Get-CertificatePin') {
    throw 'The remote pipeline must not use a local product-signing key.'
}
if ($pipeline -match '(?m)^\s*-AdditionalBuildArguments[^\r\n]*/p:RestoreConfigFile="') {
    throw 'PowerShell argument-array values must not contain literal quotes in RestoreConfigFile.'
}
if (!$pipeline.Contains("-Platform x64 -Configuration '`$(BuildConfiguration)'") -or
    !$pipeline.Contains('/p:BuildProjectReferences=true') -or
    !$pipeline.Contains('x64\$(BuildConfiguration)\ProtectedStorage\ProtectedStorage.TrustVerifier.exe') -or
    !$pipeline.Contains('Assert-ReleaseSignature -Path $editor -TrustVerifierPath')) {
    throw 'Build and use the host x64 native verifier, including when the release payloads are ARM64.'
}
if (!$pipeline.Contains('-ReleaseStage CarrierInputs') -or
    !$pipeline.Contains("solution: 'installer\PowerToysProtectedStorage\PowerToysProtectedStorage.wixproj'") -or
    !$pipeline.Contains('/p:ProtectedStorageProductCode=$(ProtectedStorageCarrierProductCode)') -or
    !$pipeline.Contains('msbuildArchitecture: x64') -or
    !$pipeline.Contains('Get-Service -Name msiserver')) {
    throw 'CI carrier packaging must use the main installer VSBuild execution path with validated inputs and service diagnostics.'
}
$carrierProject = Get-Content "$repo\installer\PowerToysProtectedStorage\PowerToysProtectedStorage.wixproj" -Raw
if (($pipeline + $carrierProject) -match 'SuppressValidation[=>]|<SuppressIces>|/p:Ices=' -or
    !$carrierProject.Contains('<IntermediateOutputPath>obj\Carrier\$(Platform)\$(Configuration)\</IntermediateOutputPath>')) {
    throw 'Carrier ICE validation must remain enabled with isolated project-local intermediates.'
}
$installer = Get-Content "$templates\steps-build-installer-vnext.yml" -Raw
if ($installer.IndexOf('template: steps-build-protected-storage.yml') -gt $installer.IndexOf('Build VNext MSI') -or
    ([regex]::Matches($installer, '/p:ProtectedStorageTrustPolicy=microsoft-production-v1')).Count -ne 4) {
    throw 'Both MSI and both bundle scopes must consume the finalized release and explicit production trust policy.'
}
$builder = Get-Content "$repo\installer\PowerToysProtectedStorage\Build-Carrier.ps1" -Raw
if ($builder.Contains('Set-AuthenticodeSignature') -or
    !$builder.Contains('& $signTool sign /sha1 $certificate.Thumbprint /fd SHA256 /tr $TimestampServer /td SHA256 $Path') -or
    !$builder.Contains('& $signTool verify /pa /all /tw $Path')) {
    throw 'Local signing must request RFC3161 explicitly and reject missing/invalid timestamps, not use legacy PowerShell timestamping.'
}
$releaseTools = Get-Content "$repo\installer\PowerToysProtectedStorage\ReleaseTools.ps1" -Raw
$gate = Get-Content "$repo\installer\PowerToysProtectedStorage\ProtectedStorage.Release.targets" -Raw
if (($builder + $releaseTools + $gate + $installer) -match 'ExpectedSignerSha256|signer_sha256|signer\.sha256' -or
    !$builder.Contains('Write-Utf8 "$generated\signer.policy" ''microsoft-production-v1''') -or
    !$builder.Contains('format=2`napp=PowerToysProtectedStorage`nsigner_policy=microsoft-production-v1`nminimum_version=$peVersion`n') -or
    !$builder.Contains("trustPolicy = 'microsoft-production-v1'") -or
    !$gate.Contains("'`$(ProtectedStorageTrustPolicy)' != 'microsoft-production-v1'")) {
    throw 'Release resources, documents, metadata, and packaging gate must use fixed production policy, not pins.'
}
if (!$builder.Contains("'ProtectedStorage.TrustVerifier.exe'") -or
    !$builder.Contains('Assert-ReleaseClientCatalog $clients') -or
    !$releaseTools.Contains('-Operation verify-file') -or !$releaseTools.Contains('-Operation verify-detached')) {
    throw 'All production verification must use shared native trust; the verifier must never be a catalog client.'
}
$publish = [regex]::Match($pipeline, '(?s)\$destination = Join-Path.*?displayName: Verify and publish finalized Protected Storage release').Value
if (!$publish -or $publish.Contains('TrustVerifier.exe') -or $publish.Contains('Get-ChildItem') -or
    !$publish.Contains('Copy-Item -LiteralPath (Join-Path $package $name) -Destination $destination')) {
    throw 'Publication must copy only the explicit product inventory, never wildcard-harvest a verifier/test executable.'
}
$timestamp = Get-Content "$repo\installer\PowerToysProtectedStorage\Add-DetachedTimestamp.ps1" -Raw
if ($timestamp.IndexOf('Assert-DetachedSignature') -gt $timestamp.IndexOf('[IO.File]::Move') -or
    !$timestamp.Contains('Assert-DetachedSignature -Path $Path -SignaturePath $pending -TrustVerifierPath $TrustVerifierPath')) {
    throw 'The native production gate must accept timestamped CMS before it replaces a release signature.'
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
Write-Host 'PASS host verifier, cycle-free signing graph, fixed policy (no pins), detached/timestamp gates, client DLL signing, and both installer scopes.'
