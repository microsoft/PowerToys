# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
[CmdletBinding()]
param(
    [switch]$Compile,
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$installer = Join-Path $repo 'installer\PowerToysSetupVNext'
$fragment = [xml](Get-Content -LiteralPath (Join-Path $installer 'ProtectedStorage.wxs') -Raw)
$ns = [Xml.XmlNamespaceManager]::new($fragment.NameTable)
$ns.AddNamespace('w', 'http://wixtoolset.org/schemas/v4/wxs')
$action = $fragment.SelectSingleNode('//w:CustomAction[@Id="RemoveProtectedStorageInstances"]', $ns)
if ($action.Execute -ne 'commit' -or $action.Impersonate -ne 'no' -or $action.ExeCommand -ne 'machine-remove --keep-data' -or $action.Return -ne 'ignore') {
    throw 'Machine removal must be a final, SYSTEM-only, best-effort fixed operation.'
}
$scheduled = $fragment.SelectSingleNode('//w:InstallExecuteSequence/w:Custom[@Action="RemoveProtectedStorageInstances"]', $ns)
if ($scheduled.Condition -notmatch 'NOT UPGRADINGPRODUCTCODE' -or $scheduled.Condition -notmatch 'REMOVE' -or $scheduled.After -ne 'RemoveFiles') {
    throw 'Machine removal must not execute during a major upgrade or before the main removal transaction.'
}
$binary = $fragment.SelectSingleNode('//w:Binary[@Id="PTStorageRemoval"]', $ns)
if ($binary.SourceFile -cne '$(var.ProtectedStorageLifecyclePath)' -or $action.BinaryRef -ne $binary.Id) {
    throw 'Machine removal must execute an embedded release Binary, not a user-selected installed path.'
}
$raw = Get-Content -LiteralPath (Join-Path $installer 'ProtectedStorage.wxs') -Raw
$machineBlock = [regex]::Match($raw, '(?s)<\?if \$\(var\.PerUser\) = "false" \?>(.*?)<\?endif\?>')
if (!$machineBlock.Success -or !$machineBlock.Groups[1].Value.Contains('RemoveProtectedStorageInstances')) {
    throw 'The privileged global cleanup action must only exist in per-machine authoring.'
}
$component = $fragment.SelectSingleNode('//w:Component[@Id="ProtectedStorageSetupComponent"]', $ns)
if (!$component.SelectSingleNode('.//w:RegistryValue[@KeyPath="yes"]', $ns)) {
    throw 'The per-user compatible Setup component requires a registry key path.'
}
$product = [xml](Get-Content -LiteralPath (Join-Path $installer 'Product.wxs') -Raw)
$productNs = [Xml.XmlNamespaceManager]::new($product.NameTable)
$productNs.AddNamespace('w', 'http://wixtoolset.org/schemas/v4/wxs')
if (!$product.SelectSingleNode('//w:Feature/w:ComponentGroupRef[@Id="ProtectedStorageComponents"]', $productNs)) {
    throw 'Main product does not include the finalized protected-storage Setup component.'
}
$project = [xml](Get-Content -LiteralPath (Join-Path $installer 'PowerToysInstallerVNext.wixproj') -Raw)
if (!$project.SelectSingleNode('/Project/Target[@Name="VerifyProtectedStoragePayloadForMainInstaller" and @DependsOnTargets="RequireProtectedStorageRelease"]')) {
    throw 'Signed release validation is not a main-installer build prerequisite.'
}
$inventoryScript = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'New-ProtectedStorageClientInventory.ps1') -Raw
if (!$inventoryScript.Contains("role = 'workspaces.arranger'") -or $inventoryScript.Contains('workspaces.plan-reader')) {
    throw 'The signed catalog must use the frozen identity-only arranger role.'
}
$contractSources = @(
    'src\common\ProtectedStorage\ProtectedStorage.Common\Authentication.cpp',
    'src\modules\Workspaces\WorkspacesLib\WorkspaceLaunchSession.cpp',
    'installer\PowerToysProtectedStorage\Build-Carrier.ps1'
)
foreach ($source in $contractSources) {
    $text = Get-Content -LiteralPath (Join-Path $repo $source) -Raw
    if (!$text.Contains('workspaces.arranger') -or $text.Contains('workspaces.plan-reader')) {
        throw "Arranger identity role is inconsistent across producer/server/consumer: $source"
    }
}
$harvest = Get-Content -LiteralPath (Join-Path $installer 'generateAllFileComponents.ps1') -Raw
if (!$harvest.Contains('$fileExclusionList += @("PowerToys.ProtectedStorageSetup.exe")')) {
    throw 'Setup would be duplicated by the generic root harvest.'
}
$bundle = Get-Content -LiteralPath (Join-Path $installer 'PowerToys.wxs') -Raw
if (!$bundle.Contains('<Payload Name="PowerToys.ProtectedStorageSetup.exe" SourceFile="$(var.ProtectedStorageSetupPath)"') -or
    !$bundle.Contains('<Variable Name="ProtectedStoragePerUserBundle" Type="numeric" Value="1" />')) {
    throw 'The per-user outer bundle lacks its verified cleanup payload or scope marker.'
}
$callback = Get-Content -LiteralPath (Join-Path $installer 'SilentFilesInUseBA\SilentFilesInUseBAFunctions.cpp') -Raw
if (!$callback.Contains('ShouldRemoveOwnerCarrier') -or !$callback.Contains('RemoveProtectedStorageForOwner') -or
    !$callback.Contains('m_removeOwner && SUCCEEDED(hrStatus) && !m_removeAttempted')) {
    throw 'Owner removal must run once after successful outer MSI execution and never during upgrade removal.'
}
$solution = [xml](Get-Content -LiteralPath (Join-Path $repo 'PowerToys.slnx') -Raw)
$newProjects = @($solution.SelectNodes('//Project') | Where-Object { $_.Path -like '*ProtectedStorage/*' })
if ($newProjects.Count -ne 11) { throw "Expected 11 native/managed protected-storage projects in main solution, got $($newProjects.Count)." }
foreach ($projectNode in $newProjects) {
    if (!(Test-Path -LiteralPath (Join-Path $repo $projectNode.Path))) { throw "Missing solution project: $($projectNode.Path)" }
}
if ($Compile) {
    $nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
    $wix = Join-Path $nugetRoot 'wixtoolset.sdk\5.0.2\tools\net6.0\wix.dll'
    if (!(Test-Path -LiteralPath $wix)) { throw "Restore the existing WiX SDK dependency before authoring compilation: $wix" }
    $binaryPath = Join-Path $repo "$Platform\$Configuration\ProtectedStorage\PowerToys.ProtectedStorageSetup.exe"
    $lifecyclePath = Join-Path $repo "$Platform\$Configuration\ProtectedStorage\PowerToys.ProtectedStorageLifecycle.exe"
    if (!(Test-Path -LiteralPath $binaryPath)) { throw 'Build the native Setup before compiling the non-installable fixture.' }
    $scratch = Join-Path $repo ('artifacts\ProtectedStorageMainAuthoring-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratch | Out-Null
    Push-Location $installer
    try {
        foreach ($perUser in @('true', 'false')) {
            & dotnet $wix build (Join-Path $PSScriptRoot 'tests\ProtectedStorageMainInstallerFixture.wxs') `
                (Join-Path $installer 'ProtectedStorage.wxs') `
                -arch $Platform.ToLowerInvariant() -d "PerUser=$perUser" -d "Platform=$Platform" -d 'Version=0.0.1' `
                -d "Configuration=$Configuration" -d "ProjectDir=$installer\" `
                -d "ProtectedStorageSetupPath=$binaryPath" -d "ProtectedStorageLifecyclePath=$lifecyclePath" `
                -pdbtype none -o (Join-Path $scratch "$perUser.msi")
            if ($LASTEXITCODE) { throw "Main installer fragment failed WiX compilation for PerUser=$perUser ($LASTEXITCODE)." }
        }
        $baFunction = Join-Path $repo "$Platform\$Configuration\SilentFilesInUseBAFunction.dll"
        if (!(Test-Path -LiteralPath $baFunction)) {
            $baFunction = Join-Path $installer "SilentFilesInUseBA\$Platform\$Configuration\SilentFilesInUseBAFunction.dll"
        }
        if (!(Test-Path -LiteralPath $baFunction)) { throw 'Build the outer bundle function before its authoring fixture.' }
        $extension = Join-Path $nugetRoot 'wixtoolset.bal.wixext\5.0.2\wixext5\WixToolset.BootstrapperApplications.wixext.dll'
        & dotnet $wix build (Join-Path $PSScriptRoot 'tests\ProtectedStorageBundleFixture.wxs') `
            -arch $Platform.ToLowerInvariant() -ext $extension -d "BAFunction=$baFunction" -d "Setup=$binaryPath" `
            -d "FixtureMsi=$(Join-Path $scratch 'true.msi')" -pdbtype none -o (Join-Path $scratch 'bundle.exe')
        if ($LASTEXITCODE) { throw "Outer bundle authoring fixture failed ($LASTEXITCODE)." }
    }
    finally {
        Pop-Location
        foreach ($file in Get-ChildItem -LiteralPath $scratch -File) { Remove-Item -LiteralPath $file.FullName -Force }
        Remove-Item -LiteralPath $scratch
    }
}
Write-Host 'PASS main-installer integration: release gate, single Setup component, machine-only embedded commit cleanup, upgrade guard, catalog role and solution references.'
