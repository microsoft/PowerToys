# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
[CmdletBinding()]
param(
    [ValidateSet('Prepare','Build','Repair','Launch','Stop','Reset','Clean','Inspect')][string]$Mode = 'Prepare',
    [ValidateSet('x64','ARM64')][string]$Platform = 'x64',
    [string]$Version = '0.101.6005.0',
    [switch]$SkipBuild,
    [switch]$BackupBeforeReset
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$configuration = 'Debug'
$app = 'PowerToysProtectedStorageLocalTest'
$owner = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$testProjects = Join-Path $repo 'src\common\ProtectedStorage\tests\LocalDeployment'
$localNativeRoot = Join-Path $repo "$Platform\$configuration\ProtectedStorageLocalTest"
$native = Join-Path $localNativeRoot 'Native'
$package = Join-Path $repo "$Platform\$configuration"
$bundle = Join-Path $localNativeRoot 'Bundle'
$results = Join-Path $localNativeRoot 'Results'
$settingsRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToysLocalE2E'
. (Join-Path $PSScriptRoot 'ProtectedStorage.LocalDeployment.ps1')

function Stop-LocalPowerToys {
    $prefix = [IO.Path]::GetFullPath($package).TrimEnd('\') + '\'
    $processes = @(Get-CimInstance Win32_Process -Filter "Name LIKE 'PowerToys%.exe'" |
        Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object @{Expression={if ($_.Name -eq 'PowerToys.exe') {0} else {1}}})
    foreach ($process in $processes) {
        $identity = Invoke-CimMethod -InputObject $process -MethodName GetOwnerSid
        if ($identity.ReturnValue -ne 0 -or $identity.Sid -ne $owner) {
            throw 'A different or unverified user is using the local E2E package. No process was selected for termination.'
        }
    }
    foreach ($process in $processes) {
        $live = Get-Process -Id $process.ProcessId -ErrorAction SilentlyContinue
        if ($live -and [string]::Equals($live.Path,$process.ExecutablePath,[StringComparison]::OrdinalIgnoreCase)) {
            try {
                Stop-Process -Id $live.Id -ErrorAction Stop
            } catch {
                # Exiting Runner can close its owned children between enumeration and stop.
                if (!$live.HasExited) { throw }
            }
        }
    }
}

function Reset-LocalStorage {
    Stop-LocalPowerToys
    $verb = if ($BackupBeforeReset) { 'clean-backup' } else { 'clean' }
    Invoke-LocalStorageLifecycle $verb $native $bundle $results $owner
    $workspaces = Join-Path $settingsRoot 'Workspaces'
    if (Test-Path -LiteralPath $workspaces) {
        Assert-PlainPath $workspaces
        $links = @(Get-ChildItem -LiteralPath $workspaces -Recurse -Force |
            Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })
        if ($links.Count) { throw 'Refusing to reset a test workspace tree containing links.' }
        Remove-Item -LiteralPath $workspaces -Recurse -Force
    }
}

function Set-LocalDefaults {
    Assert-PlainPath $settingsRoot
    New-Item -ItemType Directory -Path $settingsRoot -Force | Out-Null
    $general = @{
        startup=$false; run_elevated=$false; enabled=@{Workspaces=$true}
        enable_quick_access=$true; download_updates_automatically=$false
        show_new_updates_toast_notification=$false; show_whats_new_after_updates=$false
        enable_experimentation=$false; theme='system'; show_tray_icon=$true
    }
    $general | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $settingsRoot 'settings.json') -Encoding utf8
    @{openedAtFirstLaunch=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $settingsRoot 'oobe_settings.json') -Encoding utf8
    @{last_version=$Version} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $settingsRoot 'last_version_run.json') -Encoding utf8
    $workspaceSettings = Join-Path $settingsRoot 'Workspaces'
    New-Item -ItemType Directory -Path $workspaceSettings -Force | Out-Null
    @{name='Workspaces'; version='1'; properties=@{hotkey=@{value=@{win=$true;ctrl=$true;alt=$true;shift=$false;code=192;key='`'}}}} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $workspaceSettings 'settings.json') -Encoding utf8
}

if ($Mode -ne 'Build' -and $Platform -ne $env:PROCESSOR_ARCHITECTURE.Replace('AMD64','x64')) {
    throw 'Running the E2E package requires the matching host architecture.'
}
if ($Mode -eq 'Inspect') {
    Invoke-LocalStorageLifecycle inspect $native $bundle $results $owner
    return
}
if ($Mode -eq 'Clean') { Reset-LocalStorage; return }
if ($Mode -eq 'Stop') { Stop-LocalPowerToys; return }
if ($Mode -eq 'Launch') {
    & (Join-Path $native 'ProtectedStorage.LocalLifecycle.exe') --owner-process (Join-Path $package 'PowerToys.exe') powertoys '-'
    if ($LASTEXITCODE) { throw "Local PowerToys launch failed: $LASTEXITCODE" }
    return
}

if (!$SkipBuild -and $Mode -in @('Prepare','Build','Repair')) {
    Stop-LocalPowerToys
    Set-Variable -Name RepoRoot -Value $repo -Scope Script
    . (Join-Path $PSScriptRoot 'build-common.ps1')
    if (!(Ensure-VsDevEnvironment)) { throw 'Visual Studio developer environment is unavailable.' }
    New-Item -ItemType Directory -Path $package -Force | Out-Null
    New-Item -ItemType Directory -Path $bundle -Force | Out-Null
    $generated = Join-Path $localNativeRoot 'Generated'
    New-Item -ItemType Directory -Path $generated -Force | Out-Null
    Add-Type -AssemblyName System.Security.Cryptography.Pkcs
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=PowerToys Workspaces local E2E', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(7))
    try {
        $thumb = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($certificate.RawData)).ToLowerInvariant()
        [IO.File]::WriteAllText((Join-Path $generated 'LocalTestSigner.h'), "#pragma once`ninline constexpr char LocalTestSignerSha256[] = `"$thumb`";`n")
        $nativeTargets = Join-Path $testProjects 'FullPowerToys\Native.targets'
        $managedTargets = Join-Path $testProjects 'FullPowerToys\Managed.targets'
        $arguments = "/p:SolutionDir=$repo\ /p:Version=$Version /p:ProtectedStorageVersion=$Version /p:PowerToysLocalE2E=true /p:ForceImportBeforeCppTargets=$nativeTargets /p:CustomAfterMicrosoftCommonTargets=$managedTargets"
        foreach ($name in @('LocalCommon','LocalBootstrap','LocalRuntime','LocalLifecycle')) {
            RunMSBuild (Join-Path $testProjects "Native\$name.vcxproj") $arguments $Platform $configuration
        }
        foreach ($project in @(
            'src\runner\runner.vcxproj',
            'src\modules\Workspaces\WorkspacesModuleInterface\WorkspacesModuleInterface.vcxproj',
            'src\modules\Workspaces\WorkspacesLauncher\WorkspacesLauncher.vcxproj',
            'src\modules\Workspaces\WorkspacesCLI\WorkspacesCLI.vcxproj',
            'src\modules\Workspaces\WorkspacesSnapshotTool\WorkspacesSnapshotTool.vcxproj',
            'src\modules\Workspaces\WorkspacesWindowArranger\WorkspacesWindowArranger.vcxproj'
        )) {
            RunMSBuild (Join-Path $repo $project) "$arguments /restore /p:RestorePackagesConfig=true" $Platform $configuration
        }
        foreach ($project in @(
            'src\modules\Workspaces\WorkspacesEditor\WorkspacesEditor.csproj',
            'src\modules\Workspaces\WorkspacesLauncherUI\WorkspacesLauncherUI.csproj'
        )) {
            RunMSBuild (Join-Path $repo $project) "$arguments /restore /t:Publish /p:PublishDir=$package\" $Platform $configuration
        }
        $settingsProject = Join-Path $repo 'src\settings-ui\Settings.UI\PowerToys.Settings.csproj'
        RunMSBuild $settingsProject "$arguments /restore /t:Publish /p:PublishDir=$package\WinUI3Apps\" $Platform $configuration
        $quickAccessProject = Join-Path $repo 'src\settings-ui\QuickAccess.UI\PowerToys.QuickAccess.csproj'
        RunMSBuild $quickAccessProject "$arguments /restore /t:Publish /p:PublishDir=$package\WinUI3Apps\" $Platform $configuration
        foreach ($name in @('PowerToys.exe','WinUI3Apps\PowerToys.Settings.exe','WinUI3Apps\PowerToys.QuickAccess.exe','PowerToys.WorkspacesEditor.exe',
            'PowerToys.WorkspacesLauncher.exe','PowerToys.WorkspacesCLI.exe','PowerToys.WorkspacesLauncherUI.exe','PowerToys.WorkspacesSnapshotTool.exe',
            'PowerToys.WorkspacesWindowArranger.exe','PowerToys.WorkspacesModuleInterface.dll')) {
            $file = Get-Item -LiteralPath (Join-Path $package $name)
            if ($file.VersionInfo.FileVersion -ne $Version) { throw "Mixed local build version: $name $($file.VersionInfo.FileVersion)" }
        }
        foreach ($name in @('Bootstrap.exe','Runtime.exe')) { Copy-Item -LiteralPath (Join-Path $native $name) -Destination $bundle -Force }
        $entries = @(
            @('PowerToys.WorkspacesEditor.exe','workspaces.writer'),
            @('PowerToys.WorkspacesEditor.exe','workspaces.preview'),
            @('PowerToys.WorkspacesLauncher.exe','workspaces.launcher'),
            @('PowerToys.WorkspacesCLI.exe','workspaces.launcher'),
            @('PowerToys.WorkspacesLauncher.exe','workspaces.preview'),
            @('PowerToys.WorkspacesSnapshotTool.exe','workspaces.preview')
        )
        $clients = foreach ($entry in $entries) {
            @{image=$entry[0]; role=$entry[1]; version=$Version; sha256=(Get-FileHash -LiteralPath (Join-Path $package $entry[0]) -Algorithm SHA256).Hash.ToLowerInvariant()}
        }
        $catalog = @{format=1;app=$app;version=$Version;clients=@($clients)} | ConvertTo-Json -Depth 5 -Compress
        [IO.File]::WriteAllText((Join-Path $bundle 'ClientCatalog.json'),$catalog,[Text.UTF8Encoding]::new($false))
        $manifest = "format=1`napp=$app`nversion=$Version`n"
        foreach ($entry in @(@('bootstrap_sha256','Bootstrap.exe'),@('runtime_sha256','Runtime.exe'),@('catalog_sha256','ClientCatalog.json'))) {
            $manifest += "$($entry[0])=$((Get-FileHash -LiteralPath (Join-Path $bundle $entry[1]) -Algorithm SHA256).Hash.ToLowerInvariant())`n"
        }
        [IO.File]::WriteAllText((Join-Path $bundle 'manifest.txt'),$manifest,[Text.UTF8Encoding]::new($false))
        foreach ($entry in @(@('manifest.txt','manifest.p7s'),@('ClientCatalog.json','ClientCatalog.p7s'))) {
            $cms = [Security.Cryptography.Pkcs.SignedCms]::new(
                [Security.Cryptography.Pkcs.ContentInfo]::new([IO.File]::ReadAllBytes((Join-Path $bundle $entry[0]))),$true)
            $signer = [Security.Cryptography.Pkcs.CmsSigner]::new($certificate)
            $signer.IncludeOption = [Security.Cryptography.X509Certificates.X509IncludeOption]::EndCertOnly
            $signer.DigestAlgorithm = [Security.Cryptography.Oid]::new('2.16.840.1.101.3.4.2.1')
            $cms.ComputeSignature($signer,$true)
            [IO.File]::WriteAllBytes((Join-Path $bundle $entry[1]),$cms.Encode())
        }
        & (Join-Path $PSScriptRoot 'Test-ProtectedStorageLocalTrust.ps1') `
            -Verifier (Join-Path $native 'ProtectedStorage.LocalLifecycle.exe') `
            -Document (Join-Path $bundle 'manifest.txt') -Signature (Join-Path $bundle 'manifest.p7s')
        @{version=$Version;flavor='WorkspacesLocalE2E-Debug';settingsRoot=$settingsRoot;storageApp=$app;productionInstaller=$false} |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'local-e2e.json') -Encoding utf8
    }
    finally { $certificate.Dispose(); $rsa.Dispose() }
}
if ($Mode -eq 'Build') { Write-Host "Local PowerToys package: $package"; return }
if ($Mode -eq 'Repair') {
    Stop-LocalPowerToys
    Invoke-LocalStorageLifecycle repair $native $bundle $results $owner
    Write-Host "Local PowerToys repaired without resetting workspaces or settings: $package"
    return
}
Reset-LocalStorage
Set-LocalDefaults
$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $desktop 'PowerToys - Workspaces Local E2E.lnk'))
$shortcut.TargetPath = Join-Path $package 'PowerToys.exe'
$shortcut.Arguments = '--open-settings=Workspaces'
$shortcut.WorkingDirectory = $package
$shortcut.Description = "PowerToys $Version - isolated Workspaces E2E"
$shortcut.IconLocation = (Join-Path $package 'PowerToys.exe') + ',0'
$shortcut.Save()
Write-Host "Ready: $(Join-Path $package 'PowerToys.exe')"
Write-Host 'Fresh start: no test service is provisioned. Open Workspaces and choose Set up to create it with administrator authorization.'
