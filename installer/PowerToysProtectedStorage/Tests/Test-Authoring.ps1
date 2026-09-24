# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$Compile
)
$ErrorActionPreference = 'Stop'
$installer = (Resolve-Path "$PSScriptRoot\..").Path
$repo = (Resolve-Path "$installer\..\..").Path
$xml = [xml](Get-Content "$installer\Product.wxs" -Raw)
$ns = [Xml.XmlNamespaceManager]::new($xml.NameTable)
$ns.AddNamespace('w', 'http://wixtoolset.org/schemas/v4/wxs')
$package = $xml.SelectSingleNode('/w:Wix/w:Package', $ns)
if ($package.Scope -ne 'perUser') { throw 'Carrier scope must be fixed per-user.' }
if (!$xml.SelectSingleNode('//w:MajorUpgrade', $ns)) { throw 'MajorUpgrade is required.' }
if ($xml.SelectNodes('//w:File', $ns).Count) { throw 'Live VA Code must not be MSI File-table ownership.' }
foreach ($action in $xml.SelectNodes('//w:CustomAction', $ns)) {
    if ($action.Impersonate -ne 'yes' -or $action.Return -ne 'check') { throw 'All actions must be checked ordinary-owner operations.' }
}
$guard = $xml.SelectSingleNode('//w:InstallExecuteSequence/w:Custom[@Action="RequireRemovalAuthorization"]', $ns)
if ($guard.Condition -notmatch 'NOT UPGRADINGPRODUCTCODE') { throw 'Major upgrade must not tear down the instance.' }
$binaries = @($xml.SelectNodes('//w:Binary', $ns) | ForEach-Object Id)
foreach ($name in @('Provisioner', 'MsiAction', 'Bootstrap', 'Runtime', 'Manifest', 'ManifestSignature', 'ClientCatalog', 'ClientCatalogSignature')) {
    if ($name -notin $binaries) { throw "Missing matching-MSI payload: $name" }
}
$build = Get-Content "$installer\Build-Carrier.ps1" -Raw
$actionResource = ($build -split "`r?`n" | Where-Object { $_ -match 'Write-Utf8.+\\MsiAction\.rc' })
if (@($actionResource).Count -ne 1 -or $actionResource -notmatch 'Resource 110 ' -or $actionResource -match 'ClientCatalog|manifest') {
    throw 'MsiAction must contain only its fixed trust policy, never a catalog containing its own final hash.'
}
$orderedSteps = @(
    'Sign-File "$stage\PowerToys.ProtectedStorageMsiAction.exe"',
    'Write-Utf8 "$stage\ClientCatalog.json"',
    "Build-Native 'Lifecycle'",
    'Sign-File $msi',
    "Build-Native 'ProvisionBroker'",
    "Build-Native 'Setup'"
)
$previous = -1
foreach ($step in $orderedSteps) {
    $position = $build.IndexOf($step, [StringComparison]::Ordinal)
    if ($position -le $previous) { throw "Release signing/resource graph order is invalid: $step" }
    $previous = $position
}
if ($Compile) {
    $identity = [Diagnostics.FileVersionInfo]::GetVersionInfo("$repo\$Platform\$Configuration\ProtectedStorage\PowerToys.ProtectedStorageSetup.exe")
    if ($identity.CompanyName -cne 'Microsoft Corporation' -or
        $identity.ProductName -cne 'PowerToys (Preview) Protected Storage' -or
        $identity.OriginalFilename -cne 'PowerToys.ProtectedStorageSetup.exe') {
        throw 'Setup version-resource identity does not match the dedicated updater gate.'
    }
    foreach ($name in @('PowerToys.ProtectedStorageMsiAction.exe', 'PowerToys.ProtectedStorageLifecycle.exe',
        'PowerToys.ProtectedStorageProvisionBroker.exe')) {
        $helper = [Diagnostics.FileVersionInfo]::GetVersionInfo("$repo\$Platform\$Configuration\ProtectedStorage\$name")
        if ($helper.FileVersion -ne $identity.FileVersion -or $helper.ProductVersion -ne $identity.ProductVersion -or
            $helper.CompanyName -cne 'Microsoft Corporation') {
            throw "Installer helper must have the matching release version resource: $name"
        }
    }
    $scratch = Join-Path $PSScriptRoot ("Authoring-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory $scratch | Out-Null
    try {
        foreach ($name in @('PowerToys.ProtectedStorageLifecycle.exe', 'PowerToys.ProtectedStorageMsiAction.exe')) {
            Copy-Item "$repo\$Platform\$Configuration\ProtectedStorage\$name" $scratch
        }
        # This is an authoring-only artifact: no valid CMS, no Authenticode, no
        # release policy, no runnable Setup. It is deleted without installation.
        Copy-Item "$repo\$Platform\$Configuration\ProtectedStorage\PowerToys.ProtectedStorageMsiAction.exe" "$scratch\Bootstrap.exe"
        Copy-Item "$scratch\Bootstrap.exe" "$scratch\Runtime.exe"
        foreach ($name in @('manifest.txt', 'manifest.p7s', 'ClientCatalog.json', 'ClientCatalog.p7s')) {
            [IO.File]::WriteAllText("$scratch\$name", 'non-installable-authoring-test')
        }
        . "$repo\tools\build\build-common.ps1"
        Set-Variable -Name RepoRoot -Value $repo -Scope Script
        if (!(Ensure-VsDevEnvironment)) { throw 'Visual Studio developer environment is unavailable.' }
        RestoreThenBuild "$installer\PowerToysProtectedStorage.wixproj" `
            "/nr:false /p:ProtectedStorageStage=$scratch /p:ProtectedStorageProductCode={$([guid]::NewGuid())} /p:ProtectedStorageVersion=0.0.1" `
            $Platform $Configuration
        if (!(Test-Path "$scratch\PowerToys.ProtectedStorage.Carrier.msi")) { throw 'No compile artifact produced.' }
    } finally {
        Remove-Item -LiteralPath $scratch -Recurse -Force
    }
}
Write-Host 'PASS carrier authoring: owner scope, major upgrade, impersonated transaction, matching MSI Binary inventory, acyclic signing/resource graph.'
