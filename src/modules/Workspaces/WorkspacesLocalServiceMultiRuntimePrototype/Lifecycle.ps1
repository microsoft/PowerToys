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
if (-not (Test-Path -LiteralPath $controller -PathType Leaf)) {
    throw "Controller is missing: $controller"
}
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw "Package metadata is missing: $metadataPath"
}
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json

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

$installRoot = Join-Path `
    $env:ProgramFiles `
    'PowerToys\WorkspacesUnpackagedUpdaterVirtualRuntimePrototype'
$installedUpdater = Join-Path $installRoot 'PtPuvrUpdater.exe'
$storeRoot = Join-Path `
    $env:ProgramData `
    'Microsoft\PowerToys\WorkspacesUnpackagedUpdaterVirtualRuntimePrototype'
$legacyStoreRoot = Join-Path `
    $env:ProgramData `
    'Microsoft\PowerToys\WorkspacesPackagedUpdaterVirtualRuntimePrototype'
$legacyPackageFullNames = @(
    'Microsoft.PowerToys.WsPuvr.Updater_5.0.0.0_x64__t8ed0av59w5q6',
    'Microsoft.PowerToys.WsPuvr.Runtime1_1.0.0.0_x64__t8ed0av59w5q6',
    'Microsoft.PowerToys.WsPuvr.Runtime2_2.0.0.0_x64__t8ed0av59w5q6'
)
$exactPackageFullNames = @(
    $metadata.runtimes.track1.fullName
    $metadata.runtimes.track2.fullName
    $legacyPackageFullNames
)

function Get-RuntimeServiceName([string]$ownerSid) {
    $bytes = [Text.Encoding]::Unicode.GetBytes($ownerSid)
    $digest = [Security.Cryptography.SHA256]::HashData($bytes)
    return 'PtPuvrRuntime_' + (
        [Convert]::ToHexString($digest).ToLowerInvariant().Substring(0, 16))
}

function Read-Evidence([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Evidence is missing: $path"
    }
    $result = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $path) {
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

function Remove-ExactPackage([string]$packageFullName) {
    & $controller `
        --remove-package `
        --package-full-name $packageFullName
    $exitCode = $LASTEXITCODE
    $packageDirectory = Join-Path `
        (Join-Path $env:ProgramFiles 'WindowsApps') `
        $packageFullName
    for ($attempt = 0; $attempt -lt 40 -and
         (Test-Path -LiteralPath $packageDirectory); $attempt++) {
        Start-Sleep -Milliseconds 250
    }
    if ($exitCode -ne 0 -or (Test-Path -LiteralPath $packageDirectory)) {
        throw "Exact package removal failed for ${packageFullName}: controller exit $exitCode"
    }
}

function Start-Updater {
    $firstBundle = $metadata.simulatedBundles.'PowerToys-0.101'
    $secondBundle = $metadata.simulatedBundles.'PowerToys-0.110'
    Assert-Equal `
        $firstBundle.updaterSha256 `
        $secondBundle.updaterSha256 `
        'shared updater bundle hash'
    Assert-Equal `
        $firstBundle.updaterSha256 `
        $metadata.updater.sha256 `
        'canonical updater hash'
    Assert-Equal $metadata.updater.artifactType 'unpackaged-pe' 'updater artifact type'

    $signature = Get-AuthenticodeSignature -LiteralPath $firstBundle.updaterPath
    Assert-Equal $signature.Status 'Valid' 'updater source signature'
    Assert-Equal `
        $signature.SignerCertificate.Subject `
        $metadata.updater.signerSubject `
        'updater signer subject'

    foreach ($updaterSource in $firstBundle.updaterPath, $secondBundle.updaterPath) {
        & $controller `
            --bootstrap-install `
            --updater-binary $updaterSource
        if ($LASTEXITCODE -ne 0) {
            throw "Updater bootstrap failed for ${updaterSource}: $LASTEXITCODE"
        }
    }
}

function Provision-Two {
    & $controller `
        --provision `
        --owner-sid $FirstOwnerSid `
        --runtime-track 1 `
        --runtime-package $metadata.simulatedBundles.'PowerToys-0.101'.runtimePath
    if ($LASTEXITCODE -ne 0) {
        throw "First runtime provision failed: $LASTEXITCODE"
    }
    & $controller `
        --provision `
        --owner-sid $SecondOwnerSid `
        --runtime-track 2 `
        --runtime-package $metadata.simulatedBundles.'PowerToys-0.110'.runtimePath
    if ($LASTEXITCODE -ne 0) {
        throw "Second runtime provision failed: $LASTEXITCODE"
    }
}

function Show-Status {
    & $controller --status --owner-sid $FirstOwnerSid
    if ($LASTEXITCODE -ne 0) {
        throw "First runtime status failed: $LASTEXITCODE"
    }
    & $controller --status --owner-sid $SecondOwnerSid
    if ($LASTEXITCODE -ne 0) {
        throw "Second runtime status failed: $LASTEXITCODE"
    }
}

function Remove-All {
    $updater = Get-Service -Name PtPuvrUpdater -ErrorAction SilentlyContinue
    if ($updater -and $updater.Status -ne 'Running') {
        try {
            Start-Service -Name PtPuvrUpdater
            $updater.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
        }
        catch {
            Write-Warning "Could not start updater for managed cleanup: $($_.Exception.Message)"
        }
    }

    if ((Get-Service -Name PtPuvrUpdater -ErrorAction SilentlyContinue).Status -eq 'Running') {
        foreach ($owner in $FirstOwnerSid, $SecondOwnerSid) {
            & $controller --cleanup --owner-sid $owner
            if ($LASTEXITCODE -ne 0 -and
                $LASTEXITCODE -ne 1060 -and
                $LASTEXITCODE -ne 1168) {
                Write-Warning "Updater cleanup failed for ${owner}: $LASTEXITCODE"
            }
        }
    }

    foreach ($owner in $FirstOwnerSid, $SecondOwnerSid) {
        $serviceName = Get-RuntimeServiceName $owner
        $runtime = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
        if ($runtime) {
            if ($runtime.Status -ne 'Stopped') {
                Stop-Service -Name $serviceName -Force
                $runtime.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
            }
            sc.exe delete $serviceName | Out-Host
        }
    }

    $updater = Get-Service -Name PtPuvrUpdater -ErrorAction SilentlyContinue
    if ($updater) {
        if ($updater.Status -ne 'Stopped') {
            Stop-Service -Name PtPuvrUpdater -Force
            $updater.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }
        sc.exe delete PtPuvrUpdater | Out-Host
    }

    foreach ($packageFullName in $exactPackageFullNames) {
        Remove-ExactPackage $packageFullName
    }

    Remove-Item -LiteralPath $installRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $storeRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $legacyStoreRoot -Recurse -Force -ErrorAction SilentlyContinue
}

function Invoke-Validation {
    $firstName = Get-RuntimeServiceName $FirstOwnerSid
    $secondName = Get-RuntimeServiceName $SecondOwnerSid
    $resultPath = Join-Path $root 'artifacts\validation-result.json'
    try {
        Start-Updater
        Provision-Two
        Show-Status

        $updaterService = Get-CimInstance Win32_Service -Filter "Name='PtPuvrUpdater'"
        if (-not $updaterService) {
            throw 'Ordinary updater service is missing.'
        }
        Assert-Equal $updaterService.StartName 'LocalSystem' 'updater account'
        Assert-Equal $updaterService.State 'Running' 'updater state'
        Assert-Equal $updaterService.StartMode 'Auto' 'updater start mode'
        if ($updaterService.ProcessId -eq 0) {
            throw 'Updater PID is zero.'
        }
        $configuredUpdater = $updaterService.PathName.Trim('"')
        Assert-Equal `
            ([IO.Path]::GetFullPath($configuredUpdater)) `
            ([IO.Path]::GetFullPath($installedUpdater)) `
            'updater protected ImagePath'

        $updaterEvidence = Read-Evidence (Join-Path $storeRoot 'updater-evidence.txt')
        Assert-Equal $updaterEvidence.processId $updaterService.ProcessId 'updater evidence PID'
        Assert-Equal $updaterEvidence.tokenUserSid 'S-1-5-18' 'updater token SID'
        Assert-Equal $updaterEvidence.packageIdentityPresent 'false' 'updater package identity'
        Assert-Equal $updaterEvidence.packageIdentityError '15700' 'updater package identity error'
        Assert-Equal $updaterEvidence.updaterVersion '5.0.0.0' 'updater standalone version'
        Assert-Equal $updaterEvidence.fileVersion '5.0.0.0' 'updater file version'
        Assert-Equal `
            $updaterEvidence.deploymentMode `
            'direct-unpackaged-package-manager' `
            'updater deployment mode'
        Assert-Equal `
            ([IO.Path]::GetFullPath($updaterEvidence.executablePath)) `
            ([IO.Path]::GetFullPath($installedUpdater)) `
            'updater evidence executable'

        $installedSignature = Get-AuthenticodeSignature -LiteralPath $installedUpdater
        Assert-Equal $installedSignature.Status 'Valid' 'installed updater signature'
        Assert-Equal `
            $installedSignature.SignerCertificate.Subject `
            $metadata.updater.signerSubject `
            'installed updater signer subject'
        $installAcl = Get-Acl -LiteralPath $installRoot
        Assert-Equal $installAcl.AreAccessRulesProtected $true 'updater install DACL protection'

        $stageResults = @(
            Read-Evidence (Join-Path $storeRoot 'direct-stage-result-track1.txt')
            Read-Evidence (Join-Path $storeRoot 'direct-stage-result-track2.txt')
        )
        for ($index = 0; $index -lt $stageResults.Count; $index++) {
            $stage = $stageResults[$index]
            Assert-Equal $stage.operation 'StagePackageAsync' 'direct deployment API'
            Assert-Equal $stage.runtimeTrack ($index + 1) 'direct deployment runtime track'
            Assert-Equal $stage.callerProcessId $updaterService.ProcessId 'direct Stage caller PID'
            Assert-Equal $stage.callerTokenUserSid 'S-1-5-18' 'direct Stage caller SID'
            Assert-Equal `
                $stage.callerPackageIdentityPresent `
                'false' `
                'direct Stage caller package identity'
            Assert-Equal $stage.hresult '0x0' 'direct Stage HRESULT'
            Assert-Equal $stage.win32 '0' 'direct Stage Win32 error'
        }

        $helperArtifacts = @(
            Get-ChildItem `
                -LiteralPath $installRoot, $storeRoot, $legacyStoreRoot, (
                    Join-Path `
                        (Join-Path $env:ProgramFiles 'WindowsApps') `
                        $legacyPackageFullNames[0]) `
                -Filter PtPuvrDeploymentHelper.exe `
                -Recurse `
                -ErrorAction SilentlyContinue
        )
        Assert-Equal $helperArtifacts.Count 0 'deployment helper artifact count'

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
            $service = Get-CimInstance `
                Win32_Service `
                -Filter "Name='$($definition.service)'"
            if (-not $service) {
                throw "Runtime service is missing: $($definition.service)"
            }
            Assert-Equal `
                $service.StartName `
                "NT SERVICE\$($definition.service)" `
                'runtime virtual account'
            Assert-Equal $service.State 'Running' 'runtime state'
            if ($service.ProcessId -eq 0) {
                throw "Runtime PID is zero: $($definition.service)"
            }
            if ($service.PathName -notlike
                "*\WindowsApps\$($definition.metadata.fullName)\PtPuvrRuntime.exe*") {
                throw "Runtime ImagePath is not package-owned: $($service.PathName)"
            }
            $suffix = $definition.service.Substring('PtPuvrRuntime_'.Length)
            $evidence = Read-Evidence (Join-Path $storeRoot "$suffix\evidence.txt")
            Assert-Equal $evidence.ownerSid $definition.owner 'runtime owner'
            Assert-Equal $evidence.runtimeTrack $definition.track 'runtime track'
            Assert-Equal `
                $evidence.runtimeBinaryVersion `
                "$($definition.track).0.0.0" `
                'runtime file version'
            Assert-Equal `
                $evidence.packageFullName `
                $definition.metadata.fullName `
                'runtime package full name'
            Assert-Equal `
                $evidence.packageVersion `
                "$($definition.track).0.0.0" `
                'runtime package version'
            Assert-Equal `
                $evidence.packageIdentityPresent `
                'false' `
                'runtime package process identity'
            Assert-Equal `
                $evidence.tokenUserSid `
                $evidence.serviceSid `
                'virtual account primary SID'
            Assert-Equal `
                $evidence.serviceSidPresent `
                'true' `
                'runtime service SID membership'
            if ($evidence.executablePath -notlike
                "*\WindowsApps\$($definition.metadata.fullName)\PtPuvrRuntime.exe") {
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
        $installedRuntimeCopies = @(
            Get-ChildItem `
                -LiteralPath $installRoot, $storeRoot `
                -Filter PtPuvrRuntime.exe `
                -Recurse `
                -ErrorAction SilentlyContinue
        )
        Assert-Equal $installedRuntimeCopies.Count 0 'runtime EXE copy count'
        $matchingUpdaterServices = @(
            Get-Service -Name PtPuvrUpdater -ErrorAction SilentlyContinue)
        Assert-Equal $matchingUpdaterServices.Count 1 'singleton updater service count'

        $validation = [ordered]@{
            timestamp = (Get-Date).ToString('o')
            updater = [ordered]@{
                serviceName = 'PtPuvrUpdater'
                processId = [uint32]$updaterService.ProcessId
                account = $updaterService.StartName
                standaloneVersion = $updaterEvidence.updaterVersion
                fileVersion = $updaterEvidence.fileVersion
                packageIdentityPresent = $updaterEvidence.packageIdentityPresent
                packageIdentityError = $updaterEvidence.packageIdentityError
                executablePath = $updaterEvidence.executablePath
                sharedBundleSha256 = $metadata.updater.sha256
                deploymentMode = $updaterEvidence.deploymentMode
                deploymentHelperPresent = $false
                directStageResults = @(
                    foreach ($stage in $stageResults) {
                        [ordered]@{
                            runtimeTrack = [uint16]$stage.runtimeTrack
                            callerProcessId = [uint32]$stage.callerProcessId
                            callerPackageIdentityPresent =
                                $stage.callerPackageIdentityPresent
                            hresult = $stage.hresult
                            win32 = $stage.win32
                        }
                    }
                )
            }
            runtimes = $runtimeResults
            simulatedPowerToysVersions = @('0.101', '0.110')
            verdict = 'PASS'
        }
        $validation |
            ConvertTo-Json -Depth 8 |
            Set-Content -LiteralPath $resultPath -Encoding utf8NoBOM
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
