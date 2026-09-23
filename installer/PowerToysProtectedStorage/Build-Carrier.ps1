# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
#Requires -Version 7.2
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$Version,
    [switch]$Package,
    [string]$SigningCertificateThumbprint,
    [string]$ExpectedSignerSha256,
    [string]$TimestampServer,
    [string]$ClientCatalogInput,
    [string]$PublishDirectory,
    [ValidateSet('Compile', 'RuntimePayloads', 'Action', 'Payloads', 'Documents', 'Lifecycle', 'Carrier', 'Broker', 'Setup', 'Publish')]
    [string]$ReleaseStage = 'Compile',
    [string[]]$AdditionalBuildArguments = @(),
    [switch]$RunTests
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\ReleaseTools.ps1"
$Platform = if ($Platform -ieq 'ARM64') { 'ARM64' } else { 'x64' }
$Configuration = if ($Configuration -ieq 'Release') { 'Release' } else { 'Debug' }
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
$native = "$repo\src\common\ProtectedStorage"
$output = "$repo\$Platform\$Configuration\ProtectedStorage"
$stageRoot = "$repo\artifacts\ProtectedStorage\$Platform\$Configuration"
$generated = "$stageRoot\Generated"
$stage = "$stageRoot\Package"
if (!$PublishDirectory) { $PublishDirectory = "$repo\$Platform\$Configuration" }
if (!$Version) { $Version = ([xml](Get-Content "$repo\src\Version.props" -Raw)).Project.PropertyGroup.Version }
$releaseVersion = Get-CarrierVersion $Version
$Version = $releaseVersion.Msi
$parts = $Version.Split('.')
$peVersion = $releaseVersion.PE
New-Item -ItemType Directory -Force $generated, $stage | Out-Null

function Write-Utf8([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}
function Hash-File([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Build-Native([string]$Name) {
    $buildArguments = @("/p:ProtectedStorageGeneratedDir=$generated\", "/p:ProtectedStorageVersion=$peVersion",
        "/p:ProtectedStorageVersionMajor=$($parts[0])", "/p:ProtectedStorageVersionMinor=$($parts[1])",
        "/p:ProtectedStorageVersionBuild=$($parts[2])", '/p:ProtectedStorageVersionRevision=0') + $AdditionalBuildArguments
    & "$repo\tools\build\build.ps1" -Path "$native\ProtectedStorage.$Name" -Platform $Platform -Configuration $Configuration -ExtraArgs $buildArguments
    if ($LASTEXITCODE) { throw "ProtectedStorage.$Name build failed ($LASTEXITCODE)." }
}
function Resource([int]$Id, [string]$Path) {
    "$Id RCDATA `"$($Path.Replace('\', '\\'))`"`r`n"
}

# Product identity is release/architecture-specific, not owner-specific. It is
# stable across rebuilds and must never be reused for different release bytes.
$hash = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes("PowerToysProtectedStorage|$Platform|$Version"))
$guidBytes = [byte[]]$hash[0..15]
$guidBytes[7] = ($guidBytes[7] -band 0x0f) -bor 0x50
$guidBytes[8] = ($guidBytes[8] -band 0x3f) -bor 0x80
$productCode = '{' + ([guid]::new($guidBytes)).ToString().ToUpperInvariant() + '}'
Write-Utf8 "$generated\ReleaseIdentity.h" @"
#define PROTECTED_STORAGE_PRODUCT_CODE L"$productCode"
#define PROTECTED_STORAGE_VERSION L"$Version"
#define PROTECTED_STORAGE_PE_VERSION L"$peVersion"
"@

if ($Package -and $ReleaseStage -ne 'Compile') { throw 'Use either local -Package signing or one external -ReleaseStage, not both.' }
if (!$Package -and $ReleaseStage -eq 'Compile') {
    # Never accidentally inherit a runnable signed-resource build into a local build.
    foreach ($name in @('MsiAction', 'Lifecycle', 'ProvisionBroker', 'Setup')) {
        Remove-Item "$generated\$name.rc" -ErrorAction SilentlyContinue
        Build-Native $name
    }
} else {
    if ($ReleaseStage -ne 'RuntimePayloads' -and $ExpectedSignerSha256 -notmatch '^[a-fA-F0-9]{64}$') {
        throw 'Release stages require an explicit approved ExpectedSignerSha256.'
    }
    $pin = $ExpectedSignerSha256.ToLowerInvariant()
    if ($Package) {
        if (!$SigningCertificateThumbprint -or !$TimestampServer -or !$ClientCatalogInput) {
            throw 'Package requires SigningCertificateThumbprint, TimestampServer, and ClientCatalogInput. No developer certificate is generated.'
        }
        $certificate = Get-Item "Cert:\CurrentUser\My\$SigningCertificateThumbprint"
        if (!$certificate.HasPrivateKey) { throw 'Explicit signing certificate has no private key.' }
        if ((Get-CertificatePin $certificate) -ne $pin) { throw 'Explicit signer does not match the approved protected-policy pin.' }
    }
    if ($ReleaseStage -ne 'RuntimePayloads') { Write-Utf8 "$generated\signer.sha256" $pin }
    function Sign-File([string]$Path) {
        if (!$Package) {
            Write-Host "Awaiting external Authenticode signature: $Path"
            return
        }
        $result = Set-AuthenticodeSignature -FilePath $Path -Certificate $certificate -HashAlgorithm SHA256 -TimestampServer $TimestampServer
        if ($result.Status -ne 'Valid') { throw "Authenticode signing/trust failed: $Path ($($result.Status))" }
        Assert-ReleaseSignature -Path $Path -ExpectedSignerSha256 $pin
    }
    Add-Type -AssemblyName System.Security.Cryptography.Pkcs
    function Sign-Detached([string]$Path, [string]$Signature) {
        if (!$Package) {
            Write-Host "Awaiting external detached CMS signature: $Signature"
            return
        }
        $content = [Security.Cryptography.Pkcs.ContentInfo]::new([IO.File]::ReadAllBytes($Path))
        $cms = [Security.Cryptography.Pkcs.SignedCms]::new($content, $true)
        $signer = [Security.Cryptography.Pkcs.CmsSigner]::new($certificate)
        $signer.DigestAlgorithm = [Security.Cryptography.Oid]::new('2.16.840.1.101.3.4.2.1')
        $cms.ComputeSignature($signer)
        [IO.File]::WriteAllBytes($Signature, $cms.Encode())
    }
    function Require-SignedFile([string]$Path) {
        Assert-ReleaseSignature -Path $Path -ExpectedSignerSha256 $pin
        if ([IO.Path]::GetExtension($Path) -eq '.exe' -and
            [Diagnostics.FileVersionInfo]::GetVersionInfo($Path).FileVersion -ne $peVersion) {
            throw "Signed PE version does not match $peVersion`: $Path"
        }
    }
    $msi = "$stage\PowerToys.ProtectedStorage.Carrier.msi"
    if ($Package -or $ReleaseStage -in @('Payloads', 'RuntimePayloads')) {
        foreach ($name in @('Bootstrap', 'Runtime')) {
            Build-Native $name
            Copy-Item "$output\$name.exe" "$stage\$name.exe" -Force
            if ([Diagnostics.FileVersionInfo]::GetVersionInfo("$stage\$name.exe").FileVersion -ne $peVersion) {
                throw "$name PE version does not match $peVersion."
            }
            Sign-File "$stage\$name.exe"
        }
    }
    if ($Package -or $ReleaseStage -in @('Payloads', 'Action')) {
        # Compile/sign the action BEFORE catalog generation. No self-hash cycle:
        # payloads and caller catalog are separate MSI Binary streams.
        Write-Utf8 "$generated\MsiAction.rc" (Resource 110 "$generated\signer.sha256")
        Build-Native 'MsiAction'
        Copy-Item "$output\PowerToys.ProtectedStorageMsiAction.exe" $stage -Force
        Sign-File "$stage\PowerToys.ProtectedStorageMsiAction.exe"
    }
    if ($Package -or $ReleaseStage -eq 'Documents') {
        foreach ($name in @('Bootstrap.exe', 'Runtime.exe', 'PowerToys.ProtectedStorageMsiAction.exe')) {
            Require-SignedFile "$stage\$name"
        }
        if (!$ClientCatalogInput) { throw 'Documents requires the final signed client inventory.' }
        $catalogSource = Get-Content -LiteralPath $ClientCatalogInput -Raw | ConvertFrom-Json
        $clients = @()
        $roles = @('workspaces.writer', 'workspaces.reader', 'workspaces.launcher', 'workspaces.preview', 'workspaces.arranger')
        $reservedImages = @('Bootstrap.exe', 'Runtime.exe', 'PowerToys.ProtectedStorageSetup.exe',
            'PowerToys.ProtectedStorageProvisionBroker.exe', 'PowerToys.ProtectedStorageLifecycle.exe',
            'PowerToys.ProtectedStorageMsiAction.exe')
        foreach ($entry in $catalogSource.clients) {
            if ($entry.role -notin $roles) { throw "Unsupported catalog client role: $($entry.role)" }
            $path = (Resolve-Path -LiteralPath $entry.path).Path
            if ([IO.Path]::GetFileName($path) -in $reservedImages) {
                throw 'Lifecycle and maintenance executables cannot be supplied as data clients; this would violate the acyclic release graph.'
            }
            # Client certificates may differ (including cached core signatures).
            # Authority comes from the pinned CMS over the final image hashes.
            if ((Get-AuthenticodeSignature -LiteralPath $path).Status -ne 'Valid') {
                throw "Client is not release-signed: $path"
            }
            $clients += [ordered]@{ image = [IO.Path]::GetFileName($path); sha256 = (Hash-File $path); role = $entry.role }
        }
        if (!$clients.Count) { throw 'A production catalog must contain release clients.' }
        $clients += [ordered]@{ image = 'PowerToys.ProtectedStorageMsiAction.exe'; sha256 = (Hash-File "$stage\PowerToys.ProtectedStorageMsiAction.exe"); role = 'maintenance' }
        Write-Utf8 "$stage\ClientCatalog.json" (([ordered]@{ format = 1; app = 'PowerToysProtectedStorage'; version = $peVersion; clients = $clients }) | ConvertTo-Json -Depth 8 -Compress)
        Sign-Detached "$stage\ClientCatalog.json" "$stage\ClientCatalog.p7s"
        Write-Utf8 "$stage\manifest.txt" ("format=1`napp=PowerToysProtectedStorage`nversion=$peVersion`nbootstrap_sha256=$(Hash-File "$stage\Bootstrap.exe")`nruntime_sha256=$(Hash-File "$stage\Runtime.exe")`ncatalog_sha256=$(Hash-File "$stage\ClientCatalog.json")`n")
        Sign-Detached "$stage\manifest.txt" "$stage\manifest.p7s"
        Write-Utf8 "$stage\policy.txt" "format=1`napp=PowerToysProtectedStorage`nsigner_sha256=$pin`nminimum_version=$peVersion`n"
    }
    if ($Package -or $ReleaseStage -eq 'Lifecycle') {
        foreach ($name in @('Bootstrap.exe', 'Runtime.exe', 'PowerToys.ProtectedStorageMsiAction.exe')) {
            Require-SignedFile "$stage\$name"
        }
        Assert-ReleaseDocuments -PackageRoot $stage -Version $Version -ExpectedSignerSha256 $pin
        $lifecycleResources = (Resource 101 "$stage\Bootstrap.exe") + (Resource 102 "$stage\Runtime.exe") + (Resource 103 "$stage\policy.txt") +
            (Resource 104 "$stage\manifest.txt") + (Resource 105 "$stage\manifest.p7s") +
            (Resource 106 "$stage\ClientCatalog.json") + (Resource 107 "$stage\ClientCatalog.p7s") + (Resource 110 "$generated\signer.sha256")
        Write-Utf8 "$generated\Lifecycle.rc" $lifecycleResources
        Build-Native 'Lifecycle'
        Copy-Item "$output\PowerToys.ProtectedStorageLifecycle.exe" $stage -Force
        Sign-File "$stage\PowerToys.ProtectedStorageLifecycle.exe"
    }
    if ($Package -or $ReleaseStage -eq 'Carrier') {
        Require-SignedFile "$stage\PowerToys.ProtectedStorageLifecycle.exe"
        $buildArguments = @("/p:ProtectedStorageStage=$stage", "/p:ProtectedStorageProductCode=$productCode", "/p:ProtectedStorageVersion=$Version") + $AdditionalBuildArguments
        & "$repo\tools\build\build.ps1" -Path $PSScriptRoot -Platform $Platform -Configuration $Configuration -ExtraArgs $buildArguments
        if ($LASTEXITCODE) { throw "Carrier MSI build failed ($LASTEXITCODE)." }
        Sign-File $msi
    }
    if ($Package -or $ReleaseStage -eq 'Broker') {
        Require-SignedFile $msi
        Write-Utf8 "$generated\ProvisionBroker.rc" ((Resource 101 $msi) + (Resource 110 "$generated\signer.sha256"))
        Build-Native 'ProvisionBroker'
        Copy-Item "$output\PowerToys.ProtectedStorageProvisionBroker.exe" $stage -Force
        Sign-File "$stage\PowerToys.ProtectedStorageProvisionBroker.exe"
    }
    if ($Package -or $ReleaseStage -eq 'Setup') {
        Require-SignedFile $msi
        Require-SignedFile "$stage\PowerToys.ProtectedStorageProvisionBroker.exe"
        Write-Utf8 "$generated\Setup.rc" ((Resource 101 "$stage\PowerToys.ProtectedStorageProvisionBroker.exe") + (Resource 102 $msi) + (Resource 110 "$generated\signer.sha256"))
        Build-Native 'Setup'
        Copy-Item "$output\PowerToys.ProtectedStorageSetup.exe" $stage -Force
        Sign-File "$stage\PowerToys.ProtectedStorageSetup.exe"
    }
    if ($Package -or $ReleaseStage -eq 'Publish') {
        Require-SignedFile $msi
        foreach ($name in @('Bootstrap.exe', 'Runtime.exe', 'PowerToys.ProtectedStorageMsiAction.exe',
            'PowerToys.ProtectedStorageLifecycle.exe', 'PowerToys.ProtectedStorageProvisionBroker.exe', 'PowerToys.ProtectedStorageSetup.exe')) {
            Require-SignedFile "$stage\$name"
        }
        Assert-ReleaseDocuments -PackageRoot $stage -Version $Version -ExpectedSignerSha256 $pin
        Write-Utf8 "$stage\release.json" (([ordered]@{ productCode = $productCode; upgradeCode = '{9A6A81D4-D538-4D8A-9B30-602C38B1BA23}'; version = $Version; peVersion = $peVersion; platform = $Platform; signerSha256 = $pin; carrierSha256 = (Hash-File $msi); setupSha256 = (Hash-File "$stage\PowerToys.ProtectedStorageSetup.exe"); lifecycleSha256 = (Hash-File "$stage\PowerToys.ProtectedStorageLifecycle.exe") }) | ConvertTo-Json)
    }
}
if ($RunTests) {
    & "$repo\tools\build\build.ps1" -Path "$PSScriptRoot\Tests" -Platform $Platform -Configuration $Configuration
    if ($LASTEXITCODE) { throw 'Installer tests build failed.' }
    & "$output\ProtectedStorage.InstallerTests.exe"
    if ($LASTEXITCODE) { throw 'Installer tests failed.' }
}
if ($Package -or $ReleaseStage -eq 'Publish') {
    & "$PSScriptRoot\Verify-Release.ps1" -PackageRoot $stage -ExpectedSignerSha256 $ExpectedSignerSha256 -Version $Version -Platform $Platform
    New-Item -ItemType Directory -Force $PublishDirectory | Out-Null
    $published = Join-Path $PublishDirectory 'PowerToys.ProtectedStorageSetup.exe'
    $pending = Join-Path $PublishDirectory ("PowerToys.ProtectedStorageSetup." + [guid]::NewGuid().ToString('N') + '.pending')
    try {
        Copy-Item -LiteralPath "$stage\PowerToys.ProtectedStorageSetup.exe" -Destination $pending
        if ((Hash-File $pending) -ne (Hash-File "$stage\PowerToys.ProtectedStorageSetup.exe")) {
            throw 'Finalized Setup publication hash mismatch.'
        }
        [IO.File]::Move($pending, $published, $true)
    } finally {
        Remove-Item -LiteralPath $pending -ErrorAction SilentlyContinue
    }
    & "$PSScriptRoot\Verify-Release.ps1" -PackageRoot $stage -ExpectedSignerSha256 $ExpectedSignerSha256 -Version $Version -Platform $Platform -PublishedSetupPath $published
    Write-Host "Published the single self-contained signed Setup: $published"
}
Write-Host "Protected Storage stage $ReleaseStage completed. Only -Package or -ReleaseStage Publish produces a finalized release. Native: $output; staging: $stage"
