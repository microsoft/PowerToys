[CmdletBinding()]
param(
    [ValidateSet('bootstrap', 'provision-two', 'status', 'cleanup', 'validate')]
    [string]$Verb,
    [string]$FirstOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1122',
    [string]$SecondOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1123',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$controller = Join-Path $root "artifacts\bin\x64\$Configuration\PtPuvrController.exe"
$metadataPath = Join-Path $root 'artifacts\packages\packages.json'
if (-not (Test-Path $controller)) { throw "Controller is missing: $controller" }
if (-not (Test-Path $metadataPath)) { throw "Package metadata is missing: $metadataPath" }
$metadata = Get-Content $metadataPath -Raw | ConvertFrom-Json

function Resolve-ArtifactFile(
    [string]$Directory,
    [string]$RecordedPath,
    [string]$ExpectedSha256
) {
    $fileName = [IO.Path]::GetFileName($RecordedPath)
    if ([string]::IsNullOrWhiteSpace($fileName)) {
        throw "Artifact metadata does not contain a file name: $RecordedPath"
    }
    $candidate = Join-Path $Directory $fileName
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Artifact is missing: $candidate"
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedSha256)) {
        $actualSha256 = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
        if ($actualSha256 -ne $ExpectedSha256) {
            throw "Artifact hash mismatch for ${candidate}: expected $ExpectedSha256, actual $actualSha256"
        }
    }
    return (Resolve-Path -LiteralPath $candidate).Path
}

$packageRoot = Join-Path $root 'artifacts\packages'
$bundleRoot = Join-Path $root 'artifacts\simulated-bundles'
$metadata.updater.path = Resolve-ArtifactFile `
    $packageRoot $metadata.updater.path $metadata.updater.sha256
foreach ($runtimeName in 'track1', 'track2') {
    $runtime = $metadata.runtimes.$runtimeName
    $runtime.path = Resolve-ArtifactFile $packageRoot $runtime.path $runtime.sha256
}
foreach ($bundleName in 'PowerToys-0.101', 'PowerToys-0.110') {
    $bundle = $metadata.simulatedBundles.$bundleName
    $bundleDirectory = Join-Path $bundleRoot $bundleName
    $bundle.updaterPath = Resolve-ArtifactFile `
        $bundleDirectory $bundle.updaterPath $bundle.updaterSha256
    $runtimeMetadata = $metadata.runtimes."track$($bundle.runtimeTrack)"
    $bundle.runtimePath = Resolve-ArtifactFile `
        $bundleDirectory $bundle.runtimePath $runtimeMetadata.sha256
}
$metadata.certificatePath = Resolve-ArtifactFile `
    $packageRoot $metadata.certificatePath $null
$metadataCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $metadata.certificatePath)
if ($metadataCertificate.Thumbprint -ne $metadata.certificateThumbprint) {
    throw "Test certificate thumbprint mismatch: expected $($metadata.certificateThumbprint), actual $($metadataCertificate.Thumbprint)"
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Lifecycle operations require an elevated administrator token.'
}

function Get-RuntimeServiceName([string]$ownerSid) {
    $bytes = [Text.Encoding]::Unicode.GetBytes($ownerSid)
    $digest = [Security.Cryptography.SHA256]::HashData($bytes)
    return 'PtPuvrRuntime_' + ([Convert]::ToHexString($digest).ToLowerInvariant().Substring(0, 16))
}

function Read-Evidence([string]$path) {
    if (-not (Test-Path $path)) { throw "Evidence is missing: $path" }
    $result = [ordered]@{}
    foreach ($line in Get-Content $path) {
        $separator = $line.IndexOf('=')
        if ($separator -gt 0) {
            $result[$line.Substring(0, $separator)] = $line.Substring($separator + 1)
        }
    }
    return [pscustomobject]$result
}

function Assert-Equal($actual, $expected, [string]$label) {
    if ([string]$actual -ne [string]$expected) {
        throw "${label}: expected '$expected', actual '$actual'"
    }
}

function Start-Updater {
    $firstBundle = $metadata.simulatedBundles.'PowerToys-0.101'
    $secondBundle = $metadata.simulatedBundles.'PowerToys-0.110'
    Assert-Equal $firstBundle.updaterSha256 $secondBundle.updaterSha256 'shared updater bundle hash'
    Assert-Equal $firstBundle.updaterSha256 $metadata.updater.sha256 'canonical updater hash'

    $installed = Get-AppxPackage -AllUsers -Name $metadata.updater.packageName -ErrorAction SilentlyContinue |
        Where-Object { $_.PackageFullName -eq $metadata.updater.fullName } |
        Select-Object -First 1
    if (-not $installed) {
        Add-AppxPackage -Path $firstBundle.updaterPath -ForceApplicationShutdown
    }
    & $controller --start-updater
    if ($LASTEXITCODE -ne 0) { throw "Starting updater failed: $LASTEXITCODE" }
}

function Provision-Two {
    & $controller --provision --owner-sid $FirstOwnerSid --runtime-track 1 `
        --runtime-package $metadata.simulatedBundles.'PowerToys-0.101'.runtimePath
    if ($LASTEXITCODE -ne 0) { throw "First runtime provision failed: $LASTEXITCODE" }
    & $controller --provision --owner-sid $SecondOwnerSid --runtime-track 2 `
        --runtime-package $metadata.simulatedBundles.'PowerToys-0.110'.runtimePath
    if ($LASTEXITCODE -ne 0) { throw "Second runtime provision failed: $LASTEXITCODE" }
}

function Show-Status {
    & $controller --status --owner-sid $FirstOwnerSid
    if ($LASTEXITCODE -ne 0) { throw "First runtime status failed: $LASTEXITCODE" }
    & $controller --status --owner-sid $SecondOwnerSid
    if ($LASTEXITCODE -ne 0) { throw "Second runtime status failed: $LASTEXITCODE" }
}

function Remove-All {
    foreach ($owner in $FirstOwnerSid, $SecondOwnerSid) {
        & $controller --cleanup --owner-sid $owner
        if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1060 -and $LASTEXITCODE -ne 1168) {
            throw "Runtime cleanup failed for ${owner}: $LASTEXITCODE"
        }
    }
    $updaterService = Get-Service -Name PtPuvrUpdater -ErrorAction SilentlyContinue
    if ($updaterService -and $updaterService.Status -ne 'Stopped') {
        Stop-Service -Name PtPuvrUpdater -Force
        $updaterService.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    $updaterPackage = Get-AppxPackage -AllUsers -Name $metadata.updater.packageName -ErrorAction SilentlyContinue |
        Where-Object { $_.PackageFullName -eq $metadata.updater.fullName }
    foreach ($package in $updaterPackage) {
        Remove-AppxPackage -Package $package.PackageFullName -AllUsers
    }
    $storeRoot = Join-Path $env:ProgramData 'Microsoft\PowerToys\WorkspacesPackagedUpdaterVirtualRuntimePrototype'
    Remove-Item -LiteralPath $storeRoot -Recurse -Force -ErrorAction SilentlyContinue
}

function Invoke-Validation {
    $firstName = Get-RuntimeServiceName $FirstOwnerSid
    $secondName = Get-RuntimeServiceName $SecondOwnerSid
    $storeRoot = Join-Path $env:ProgramData 'Microsoft\PowerToys\WorkspacesPackagedUpdaterVirtualRuntimePrototype'
    $resultPath = Join-Path $root 'artifacts\validation-result.json'
    try {
        Start-Updater
        Provision-Two
        Show-Status

        $updaterService = Get-CimInstance Win32_Service -Filter "Name='PtPuvrUpdater'"
        if (-not $updaterService) { throw 'Manifest-owned updater service is missing.' }
        Assert-Equal $updaterService.StartName 'LocalSystem' 'updater account'
        Assert-Equal $updaterService.State 'Running' 'updater state'
        if ($updaterService.ProcessId -eq 0) { throw 'Updater PID is zero.' }
        if ($updaterService.PathName -notlike "*\WindowsApps\$($metadata.updater.fullName)\PtPuvrUpdater.exe*") {
            throw "Updater ImagePath is not package-owned: $($updaterService.PathName)"
        }
        $updaterEvidence = Read-Evidence (Join-Path $storeRoot 'updater-evidence.txt')
        Assert-Equal $updaterEvidence.tokenUserSid 'S-1-5-18' 'updater token SID'
        Assert-Equal $updaterEvidence.packageIdentityPresent 'true' 'updater package identity'
        Assert-Equal $updaterEvidence.packageFullName $metadata.updater.fullName 'updater package full name'
        Assert-Equal $updaterEvidence.packageVersion '5.0.0.0' 'updater package version'
        Assert-Equal $updaterEvidence.fileVersion '5.0.0.0' 'updater file version'
        $inheritedHelperEvidence = Read-Evidence (
            Join-Path $storeRoot 'deployment-helper-inherited-evidence.txt')
        Assert-Equal $inheritedHelperEvidence.packageIdentityPresent 'true' `
            'default child inherited package identity'
        Assert-Equal $inheritedHelperEvidence.launchMode 'default-child' `
            'default child launch mode'
        if ($inheritedHelperEvidence.executablePath -notlike "*\WindowsApps\$($metadata.updater.fullName)\PtPuvrDeploymentHelper.exe") {
            throw "Default child is not running from the updater package: $($inheritedHelperEvidence.executablePath)"
        }
        $bridgeEvidence = Read-Evidence (
            Join-Path $storeRoot 'deployment-helper-breakaway-bridge-evidence.txt')
        Assert-Equal $bridgeEvidence.packageIdentityPresent 'true' `
            'breakaway bridge package identity'
        Assert-Equal $bridgeEvidence.launchMode `
            'desktop-app-breakaway-enable-process-tree' 'breakaway bridge launch mode'
        $breakawayEvidence = Read-Evidence (
            Join-Path $storeRoot 'deployment-helper-breakaway-evidence.txt')
        Assert-Equal $breakawayEvidence.packageIdentityPresent 'true' `
            'breakaway descendant package identity'
        Assert-Equal $breakawayEvidence.launchMode 'desktop-app-breakaway' `
            'breakaway descendant launch mode'
        if ($breakawayEvidence.executablePath -notlike "*\WindowsApps\$($metadata.updater.fullName)\PtPuvrDeploymentHelper.exe") {
            throw "Breakaway descendant is not running from the updater package: $($breakawayEvidence.executablePath)"
        }
        $breakawayResult = Read-Evidence (Join-Path $storeRoot 'breakaway-stage-result.txt')
        Assert-Equal $breakawayResult.hresult '0x80070520' 'breakaway Stage HRESULT'
        Assert-Equal $breakawayResult.win32 '1312' 'breakaway Stage Win32 error'
        $addResults = @(
            Read-Evidence (Join-Path $storeRoot 'packaged-add-result-track1.txt')
            Read-Evidence (Join-Path $storeRoot 'packaged-add-result-track2.txt')
        )
        foreach ($addResult in $addResults) {
            Assert-Equal $addResult.hresult '0x80070520' 'packaged Add HRESULT'
            Assert-Equal $addResult.win32 '1312' 'packaged Add Win32 error'
        }
        $helperEvidence = Read-Evidence (Join-Path $storeRoot 'deployment-helper-evidence.txt')
        Assert-Equal $helperEvidence.packageIdentityPresent 'false' 'deployment helper package identity'
        Assert-Equal $helperEvidence.tokenUserSid 'S-1-5-18' 'deployment helper token SID'
        Assert-Equal $helperEvidence.launchMode 'protected-cache' `
            'deployment helper launch mode'
        if ($helperEvidence.executablePath -notlike "*\ProgramData\Microsoft\PowerToys\WorkspacesPackagedUpdaterVirtualRuntimePrototype\DeploymentHelper\5.0.0.0\PtPuvrDeploymentHelper.exe") {
            throw "Deployment helper is not running from the protected updater cache: $($helperEvidence.executablePath)"
        }

        $runtimeResults = @()
        $definitions = @(
            [pscustomobject]@{
                owner = $FirstOwnerSid
                service = $firstName
                track = 1
                metadata = $metadata.runtimes.track1
            },
            [pscustomobject]@{
                owner = $SecondOwnerSid
                service = $secondName
                track = 2
                metadata = $metadata.runtimes.track2
            }
        )
        foreach ($definition in $definitions) {
            $service = Get-CimInstance Win32_Service -Filter "Name='$($definition.service)'"
            if (-not $service) { throw "Runtime service is missing: $($definition.service)" }
            Assert-Equal $service.StartName "NT SERVICE\$($definition.service)" 'runtime virtual account'
            Assert-Equal $service.State 'Running' 'runtime state'
            if ($service.ProcessId -eq 0) { throw "Runtime PID is zero: $($definition.service)" }
            if ($service.PathName -notlike "*\WindowsApps\$($definition.metadata.fullName)\PtPuvrRuntime.exe*") {
                throw "Runtime ImagePath is not package-owned: $($service.PathName)"
            }
            $suffix = $definition.service.Substring('PtPuvrRuntime_'.Length)
            $evidence = Read-Evidence (Join-Path $storeRoot "$suffix\evidence.txt")
            Assert-Equal $evidence.ownerSid $definition.owner 'runtime owner'
            Assert-Equal $evidence.runtimeTrack $definition.track 'runtime track'
            Assert-Equal $evidence.runtimeBinaryVersion "$($definition.track).0.0.0" 'runtime file version'
            Assert-Equal $evidence.packageFullName $definition.metadata.fullName 'runtime package full name'
            Assert-Equal $evidence.packageVersion "$($definition.track).0.0.0" 'runtime package version'
            Assert-Equal $evidence.packageIdentityPresent 'false' 'runtime package process identity'
            Assert-Equal $evidence.tokenUserSid $evidence.serviceSid 'virtual account primary SID'
            Assert-Equal $evidence.serviceSidPresent 'true' 'runtime service SID membership'
            if ($evidence.executablePath -notlike "*\WindowsApps\$($definition.metadata.fullName)\PtPuvrRuntime.exe") {
                throw "Runtime executable is not the direct WindowsApps payload: $($evidence.executablePath)"
            }
            $runtimeResults += [pscustomobject]@{
                ownerSid = $definition.owner
                serviceName = $definition.service
                processId = [uint32]$service.ProcessId
                runtimeTrack = [uint16]$definition.track
                packageFullName = $evidence.packageFullName
                tokenUserSid = $evidence.tokenUserSid
                serviceSid = $evidence.serviceSid
                packageIdentityPresent = $evidence.packageIdentityPresent
                executablePath = $evidence.executablePath
            }
        }
        if ($runtimeResults[0].processId -eq $runtimeResults[1].processId) {
            throw 'Runtime services unexpectedly share one PID.'
        }
        if ($runtimeResults[0].packageFullName -eq $runtimeResults[1].packageFullName) {
            throw 'Runtime services unexpectedly share one package family/version.'
        }
        $installedRuntimeCopies = Get-ChildItem -LiteralPath $storeRoot -Filter PtPuvrRuntime.exe -Recurse -ErrorAction SilentlyContinue
        if ($installedRuntimeCopies) {
            throw 'A runtime EXE copy exists outside WindowsApps in the prototype store.'
        }
        $matchingUpdaterServices = @(Get-Service -Name 'PtPuvrUpdater' -ErrorAction SilentlyContinue)
        Assert-Equal $matchingUpdaterServices.Count 1 'singleton updater service count'

        $validation = [ordered]@{
            timestamp = (Get-Date).ToString('o')
            updater = [ordered]@{
                serviceName = 'PtPuvrUpdater'
                processId = [uint32]$updaterService.ProcessId
                account = $updaterService.StartName
                packageFullName = $updaterEvidence.packageFullName
                packageVersion = $updaterEvidence.packageVersion
                fileVersion = $updaterEvidence.fileVersion
                packageIdentityPresent = $updaterEvidence.packageIdentityPresent
                sharedBundleSha256 = $metadata.updater.sha256
                defaultChildPackageIdentityPresent = $inheritedHelperEvidence.packageIdentityPresent
                breakawayBridgePackageIdentityPresent = $bridgeEvidence.packageIdentityPresent
                breakawayDescendantPackageIdentityPresent = $breakawayEvidence.packageIdentityPresent
                breakawayStageHresult = $breakawayResult.hresult
                packagedAddResults = @(
                    foreach ($addResult in $addResults) {
                        [ordered]@{
                            hresult = $addResult.hresult
                            win32 = $addResult.win32
                        }
                    }
                )
                deploymentHelperPackageIdentityPresent = $helperEvidence.packageIdentityPresent
                deploymentHelperLaunchMode = $helperEvidence.launchMode
                deploymentHelperExecutablePath = $helperEvidence.executablePath
            }
            runtimes = $runtimeResults
            simulatedPowerToysVersions = @('0.101', '0.110')
            verdict = 'PASS'
        }
        $validation | ConvertTo-Json -Depth 8 | Set-Content $resultPath -Encoding utf8NoBOM
        Write-Host "VALIDATION PASS: $resultPath"
    }
    finally {
        Remove-All
    }
}

switch ($Verb) {
    'bootstrap' { Start-Updater }
    'provision-two' { Provision-Two }
    'status' { Show-Status }
    'cleanup' { Remove-All }
    'validate' { Invoke-Validation }
}
