# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
[CmdletBinding()]
param(
    [ValidateSet('Test', 'Build', 'Deploy', 'Clean', 'Editor', 'Inspect')][string]$Action = 'Test',
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$Version = '0.101.6000.0',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$app = 'PowerToysProtectedStorageLocalTest'
$owner = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
# Keep build artifacts outside the shipping x64/ARM64 output trees.
$out = Join-Path $repo "artifacts\ProtectedStorageLocalTest\$Platform\$Configuration"
$native = Join-Path $out 'Native'
$ui = Join-Path $out 'UI'
$bundle = Join-Path $out 'Bundle'
$service = "${app}_$owner"
$testProjects = Join-Path $repo 'src\common\ProtectedStorage\tests\LocalDeployment'
if ($Action -in @('Test','Deploy','Editor') -and $Platform -ne $env:PROCESSOR_ARCHITECTURE.Replace('AMD64','x64')) {
    throw 'Live local deployment must match the host architecture; cross-builds do not qualify runtime behavior.'
}

function Assert-PlainPath([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Refusing a reparse point: $current"
            }
        }
        $parent = [IO.Directory]::GetParent($current)
        $current = if ($parent) { $parent.FullName } else { $null }
    }
}

function Stop-LocalEditor {
    foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name='PowerToys.WorkspacesEditor.exe'")) {
        if ($process.ExecutablePath -and
            [string]::Equals($process.ExecutablePath, (Join-Path $ui 'PowerToys.WorkspacesEditor.exe'), [StringComparison]::OrdinalIgnoreCase)) {
            $identity = Invoke-CimMethod -InputObject $process -MethodName GetOwnerSid
            if ($identity.ReturnValue -ne 0 -or $identity.Sid -ne $owner) { throw 'Local editor owner could not be verified.' }
            Stop-Process -Id $process.ProcessId -ErrorAction Stop
        }
    }
}

function Invoke-Lifecycle([string]$Verb) {
    if (!(Test-Path -LiteralPath (Join-Path $native 'ProtectedStorage.LocalLifecycle.exe'))) {
        throw 'Build the local-test lifecycle helper first. No installed service has been changed.'
    }
    if (![Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this local-development deployment command in an elevated PowerShell terminal.'
    }
    $stage = Join-Path $env:ProgramFiles "${app}Tools\$([guid]::NewGuid().ToString('N'))"
    Assert-PlainPath $stage
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $security = [Security.AccessControl.DirectorySecurity]::new()
    $security.SetSecurityDescriptorSddlForm('O:BAG:BAD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)')
    Set-Acl -LiteralPath $stage -AclObject $security
    $helper = Join-Path $stage 'Lifecycle.exe'
    Copy-Item -LiteralPath (Join-Path $native 'ProtectedStorage.LocalLifecycle.exe') -Destination $helper
    $broker = "${app}Provision_$([guid]::NewGuid().ToString('N'))"
    $command = "`"$helper`" --service $Verb $owner `"$bundle`""
    & sc.exe create $broker binPath= $command start= demand obj= LocalSystem | Out-Null
    if ($LASTEXITCODE) { throw "Temporary test lifecycle creation failed: $LASTEXITCODE" }
    $terminalRecorded = $false
    try {
        & sc.exe start $broker | Out-Null
        if ($LASTEXITCODE) { throw "Temporary test lifecycle start failed: $LASTEXITCODE" }
        $resultPath = Join-Path $stage 'result.json'
        $controller = Get-Service -Name $broker
        $controller.WaitForStatus([ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(90))
        if (!(Test-Path -LiteralPath $resultPath)) { throw "Lifecycle outcome unknown; inspect $stage. Do not replay." }
        $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        if ($result.ok -isnot [bool]) { throw "Malformed lifecycle result; retained evidence at $stage." }
        New-Item -ItemType Directory -Path (Join-Path $out 'Results') -Force | Out-Null
        $saved = Join-Path $out "Results\$Verb-$([DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff')).json"
        Copy-Item -LiteralPath $resultPath -Destination $saved
        $terminalRecorded = $true
        if (!$result.ok) { throw "Local lifecycle failed: $($result | ConvertTo-Json -Compress). Evidence: $saved" }
        $result | ConvertTo-Json -Compress | Write-Host
        if ($result.profileCleanupPending) {
            Write-Warning "The isolated VA profile is still in use (code $($result.profileNativeCode)). Test service/code/data were removed; the exact profile remains recorded for a later cleanup retry."
        }
    }
    finally {
        $brokerState = Get-Service -Name $broker -ErrorAction SilentlyContinue
        if ($brokerState -and $brokerState.Status -eq 'Stopped') {
            & sc.exe delete $broker | Out-Null
            if ($LASTEXITCODE) { throw "Temporary test lifecycle deletion failed: $LASTEXITCODE" }
            if ($terminalRecorded) {
                # This exact admin-owned directory was created by this invocation.
                Remove-Item -LiteralPath $stage -Recurse -Force
            } else {
                Write-Warning "Unknown lifecycle outcome; protected evidence retained at $stage."
            }
        } elseif ($brokerState) {
            Write-Warning "Lifecycle still active; retained $broker and $stage for inspection."
        }
    }
}

function Clear-LocalTest {
    Stop-LocalEditor
    Invoke-Lifecycle 'clean'
    $legacyRoot = Join-Path $env:LOCALAPPDATA "$app\Workspaces"
    if (Test-Path -LiteralPath $legacyRoot) {
        Assert-PlainPath $legacyRoot
        $links = @(Get-ChildItem -LiteralPath $legacyRoot -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })
        if ($links.Count) { throw 'Refusing to delete local-test legacy data containing a reparse point.' }
        Remove-Item -LiteralPath $legacyRoot -Recurse -Force
    }
}

function Open-LocalEditor([bool]$CloseAfterCheck) {
    $text = & (Join-Path $native 'ProtectedStorage.LocalLifecycle.exe') --owner-process (Join-Path $ui 'PowerToys.WorkspacesEditor.exe') editor '-'
    if ($LASTEXITCODE) { throw "Local editor launch failed: $LASTEXITCODE" }
    $launch = $text | ConvertFrom-Json
    $process = Get-Process -Id $launch.editorPid
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 100
            $process.Refresh()
            if ($process.HasExited) { throw 'The isolated editor exited during initialization; inspect its local-test log.' }
        } while ($process.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)
        if ($process.MainWindowTitle -notlike '*LOCAL TEST*' -or !$process.Responding) {
            throw 'The isolated editor did not reach its responsive main window.'
        }
        $result = [pscustomobject]@{editorPid=$process.Id; title=$process.MainWindowTitle; responding=$process.Responding; elevated=$launch.elevated}
        New-Item -ItemType Directory -Path (Join-Path $out 'Results') -Force | Out-Null
        $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out "Results\editor-$([guid]::NewGuid().ToString('N')).json")
        $result | ConvertTo-Json -Compress | Write-Host
    }
    finally {
        if ($CloseAfterCheck -and !$process.HasExited) {
            if (!$process.CloseMainWindow() -or !$process.WaitForExit(5000)) { Stop-Process -Id $process.Id -ErrorAction Stop }
        }
        $process.Dispose()
    }
}

if ($Action -eq 'Inspect') {
    Get-CimInstance Win32_Service -Filter "Name='$service' OR Name='PowerToysProtectedStorage_$owner'" |
        Select-Object Name,State,StartName,PathName,ProcessId | Format-List
    Invoke-Lifecycle 'inspect'
    Write-Host "Local-test artifacts: $out"
    return
}
if ($Action -eq 'Clean') { Clear-LocalTest; return }
if ($Action -eq 'Editor') {
    Open-LocalEditor $false
    return
}

Assert-PlainPath $out
New-Item -ItemType Directory -Path $out -Force | Out-Null
if (!$SkipBuild -and $Action -in @('Test', 'Build')) {
    Set-Variable -Name RepoRoot -Value $repo -Scope Script
    . (Join-Path $PSScriptRoot 'build-common.ps1')
    if (!(Ensure-VsDevEnvironment)) { throw 'Visual Studio developer environment is unavailable.' }
    Add-Type -AssemblyName System.Security.Cryptography.Pkcs
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=PowerToys isolated local development', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(7))
    try {
        $generated = Join-Path $out 'Generated'
        New-Item -ItemType Directory -Path $generated -Force | Out-Null
        $thumb = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($certificate.RawData)).ToLowerInvariant()
        [IO.File]::WriteAllText((Join-Path $generated 'LocalTestSigner.h'), "#pragma once`ninline constexpr char LocalTestSignerSha256[] = `"$thumb`";`n")
        foreach ($project in @('LocalCommon', 'LocalBootstrap', 'LocalRuntime', 'LocalLifecycle')) {
            RunMSBuild (Join-Path $testProjects "Native\$project.vcxproj") "/p:ProtectedStorageVersion=$Version" $Platform $Configuration
        }
        if ($Action -eq 'Test') { Clear-LocalTest }
        RunMSBuild (Join-Path $repo 'src\modules\Workspaces\WorkspacesEditor\WorkspacesEditor.csproj') `
            "/t:Publish /p:Version=$Version /p:PublishDir=$ui\" $Platform $Configuration
        RunMSBuild (Join-Path $testProjects 'Smoke\LocalSmoke.csproj') "/t:Restore /p:Version=$Version" $Platform $Configuration
        RunMSBuild (Join-Path $testProjects 'Smoke\LocalSmoke.csproj') "/t:Publish /p:Version=$Version /p:PublishDir=$ui\" $Platform $Configuration
        RunMSBuild (Join-Path $testProjects 'Managed\LocalManaged.csproj') "/t:Restore /p:Version=$Version" $Platform $Configuration
        RunMSBuild (Join-Path $testProjects 'Managed\LocalManaged.csproj') "/p:Version=$Version" $Platform $Configuration
        Copy-Item -LiteralPath (Join-Path $out 'Managed\PowerToys.ProtectedStorage.Client.Managed.dll') -Destination $ui -Force
        New-Item -ItemType Directory -Path $bundle -Force | Out-Null
        foreach ($name in @('Bootstrap.exe', 'Runtime.exe')) { Copy-Item -LiteralPath (Join-Path $native $name) -Destination $bundle -Force }
        $clients = foreach ($name in @('PowerToys.WorkspacesEditor.exe', 'PowerToys.ProtectedStorage.LocalSmoke.exe')) {
            $file = Join-Path $ui $name
            foreach ($role in @('workspaces.writer', 'workspaces.preview')) {
                @{ image=$name; version=(Get-Item -LiteralPath $file).VersionInfo.FileVersion; sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(); role=$role }
            }
        }
        $catalog = @{format=1; app=$app; version=$Version; clients=@($clients)} | ConvertTo-Json -Depth 5 -Compress
        [IO.File]::WriteAllText((Join-Path $bundle 'ClientCatalog.json'), $catalog, [Text.UTF8Encoding]::new($false))
        $manifest = "format=1`napp=$app`nversion=$Version`n"
        foreach ($entry in @(@('bootstrap_sha256','Bootstrap.exe'), @('runtime_sha256','Runtime.exe'), @('catalog_sha256','ClientCatalog.json'))) {
            $manifest += "$($entry[0])=$((Get-FileHash -LiteralPath (Join-Path $bundle $entry[1]) -Algorithm SHA256).Hash.ToLowerInvariant())`n"
        }
        [IO.File]::WriteAllText((Join-Path $bundle 'manifest.txt'), $manifest, [Text.UTF8Encoding]::new($false))
        foreach ($entry in @(@('manifest.txt','manifest.p7s'), @('ClientCatalog.json','ClientCatalog.p7s'))) {
            $cms = [Security.Cryptography.Pkcs.SignedCms]::new(
                [Security.Cryptography.Pkcs.ContentInfo]::new([IO.File]::ReadAllBytes((Join-Path $bundle $entry[0]))), $true)
            $signer = [Security.Cryptography.Pkcs.CmsSigner]::new($certificate)
            $signer.IncludeOption = [Security.Cryptography.X509Certificates.X509IncludeOption]::EndCertOnly
            $signer.DigestAlgorithm = [Security.Cryptography.Oid]::new('2.16.840.1.101.3.4.2.1')
            $cms.ComputeSignature($signer, $true)
            [IO.File]::WriteAllBytes((Join-Path $bundle $entry[1]), $cms.Encode())
        }
        RunMSBuild (Join-Path $repo 'src\common\ProtectedStorage\ProtectedStorage.TrustVerifier\ProtectedStorage.TrustVerifier.vcxproj') '' 'x64' $Configuration
        $verifier = Join-Path $repo "x64\$Configuration\ProtectedStorage\ProtectedStorage.TrustVerifier.exe"
        & $verifier verify-detached (Join-Path $bundle 'manifest.txt') (Join-Path $bundle 'manifest.p7s') *> (Join-Path $out 'production-rejection.log')
        if ($LASTEXITCODE -ne 1) { throw "Production verification did not produce the expected trust rejection (exit $LASTEXITCODE)." }
        $global:LASTEXITCODE = 0
        Write-Host 'Production verifier rejected the isolated test signature as required.'
    }
    finally { $certificate.Dispose(); $rsa.Dispose() }
}
elseif ($Action -eq 'Test') { Clear-LocalTest }

if ($Action -eq 'Build') { Write-Host "Non-shipping local build: $out"; return }
Invoke-Lifecycle 'deploy'
if ($Action -eq 'Test') {
    $smoke = Join-Path $ui 'PowerToys.ProtectedStorage.LocalSmoke.exe'
    foreach ($phase in @('seed', 'exercise', 'verify')) {
        $report = Join-Path $out "Results\smoke-$phase-$([guid]::NewGuid().ToString('N')).json"
        & (Join-Path $native 'ProtectedStorage.LocalLifecycle.exe') --owner-process $smoke $phase $report
        $result = $LASTEXITCODE
        if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report -Raw | Write-Host }
        if ($result) { throw "Local $phase failed ($result). Deployment retained for diagnosis." }
    }
    Open-LocalEditor $true
    Write-Host 'Local VA migration/save/reopen/preview, direct-write denial and the real editor main window passed.'
}
