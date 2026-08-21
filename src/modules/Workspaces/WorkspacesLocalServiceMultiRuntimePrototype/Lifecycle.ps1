[CmdletBinding()]
param(
    [ValidateSet('bootstrap', 'provision-two', 'status', 'cleanup', 'validate')]
    [string]$Verb,
    [string]$FirstOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1122',
    [string]$SecondOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1123',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$PreserveTrustedCertificates
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$controller = Join-Path $root "artifacts\bin\x64\$Configuration\PtPuvrController.exe"
$releaseRoot = Join-Path $root 'artifacts\release'
$metadataPath = Join-Path $releaseRoot 'artifacts.json'
$resultPath = Join-Path $root 'artifacts\validation-result.json'
$installRoot = Join-Path $env:ProgramFiles 'PowerToys\WorkspacesProtectedRuntimeUpdaterPrototype'
$storeRoot = Join-Path $env:ProgramData 'Microsoft\PowerToys\WorkspacesProtectedRuntimeUpdaterPrototype'
$ownershipPath = Join-Path $releaseRoot 'certificate-ownership.json'
$certificateStores = @(
    'Cert:\CurrentUser\My',
    'Cert:\CurrentUser\TrustedPeople',
    'Cert:\LocalMachine\My',
    'Cert:\LocalMachine\TrustedPeople'
)
$testCertificates = @()
$certificateOwnership = $null

if (-not (Test-Path -LiteralPath $controller -PathType Leaf)) {
    throw "Controller is missing: $controller"
}
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw "Release metadata is missing: $metadataPath"
}
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Lifecycle operations require an elevated administrator token.'
}

function Assert-True($Value, [string]$Label) {
    if (-not $Value) {
        throw "Assertion failed: $Label"
    }
}

function Assert-Equal($Actual, $Expected, [string]$Label) {
    if ([string]$Actual -ne [string]$Expected) {
        throw "${Label}: expected '$Expected', actual '$Actual'"
    }
}

function Get-RuntimeServiceName([string]$OwnerSid) {
    $bytes = [Text.Encoding]::Unicode.GetBytes($OwnerSid)
    $digest = [Security.Cryptography.SHA256]::HashData($bytes)
    return 'PtPuvrRuntime_' + [Convert]::ToHexString($digest).ToLowerInvariant().Substring(0, 16)
}

function Read-Evidence([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Evidence is missing: $Path"
    }
    $fields = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $separator = $line.IndexOf('=')
        if ($separator -gt 0) {
            $fields[$line.Substring(0, $separator)] = $line.Substring($separator + 1)
        }
    }
    return [pscustomobject]$fields
}

function Resolve-ReleaseFile([string]$File, [string]$ExpectedHash = '') {
    $path = Join-Path $releaseRoot $File
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Release artifact is missing: $path"
    }
    if ($ExpectedHash) {
        Assert-Equal (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash $ExpectedHash "release hash $File"
    }
    return (Resolve-Path -LiteralPath $path).Path
}

function Get-RuntimeArtifact([string]$Id) {
    $artifact = @($metadata.runtimes | Where-Object { $_.id -eq $Id })
    if ($artifact.Count -ne 1) {
        throw "Runtime artifact selection failed: $Id"
    }
    return $artifact[0]
}

function Get-CertificateSha256([Security.Cryptography.X509Certificates.X509Certificate2]$Certificate) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Certificate.RawData))
}

function Get-TestCertificates {
    $records = @(
        [ordered]@{
            role = 'primary'
            file = $metadata.certificateFile
            thumbprint = $metadata.certificateThumbprint
            signerSha256 = $metadata.trustedSignerSha256
        },
        [ordered]@{
            role = 'foreign'
            file = $metadata.foreignSignerCertificateFile
            thumbprint = $metadata.foreignSignerCertificateThumbprint
            signerSha256 = $metadata.foreignSignerSignerSha256
        }
    )
    foreach ($record in $records) {
        if ([string]::IsNullOrWhiteSpace($record.file) -or
            [string]::IsNullOrWhiteSpace($record.thumbprint) -or
            [string]::IsNullOrWhiteSpace($record.signerSha256)) {
            throw "Certificate metadata is incomplete for $($record.role)."
        }
        $path = Resolve-ReleaseFile $record.file
        $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($path)
        Assert-Equal $certificate.Thumbprint $record.thumbprint "certificate thumbprint $($record.role)"
        Assert-Equal (Get-CertificateSha256 $certificate) $record.signerSha256 "certificate SHA-256 $($record.role)"
        $record.path = $path
    }
    return $records
}

function Get-CertificateOwnership([object[]]$Certificates) {
    if ($PreserveTrustedCertificates) {
        return $null
    }
    if (-not (Test-Path -LiteralPath $ownershipPath -PathType Leaf)) {
        throw "Certificate ownership state is missing: $ownershipPath. Refusing to remove trusted certificates."
    }
    $ownership = Get-Content -LiteralPath $ownershipPath -Raw | ConvertFrom-Json
    Assert-Equal $ownership.format 1 'certificate ownership format'
    foreach ($certificate in $Certificates) {
        $record = @($ownership.certificates | Where-Object { $_.thumbprint -eq $certificate.thumbprint })
        Assert-Equal $record.Count 1 "certificate ownership record $($certificate.role)"
        foreach ($store in $certificateStores) {
            $entry = @($record[0].stores | Where-Object { $_.path -eq $store })
            Assert-Equal $entry.Count 1 "certificate ownership store $($certificate.role) $store"
            Assert-True ($entry[0].preRunPresent -is [bool]) "certificate ownership prior presence $($certificate.role) $store"
            Assert-True ($entry[0].introducedByRun -is [bool]) "certificate ownership introduction $($certificate.role) $store"
        }
    }
    return $ownership
}

function Get-CertificateOwnershipStore([object]$Certificate, [string]$Store) {
    $record = @($certificateOwnership.certificates | Where-Object { $_.thumbprint -eq $Certificate.thumbprint })
    Assert-Equal $record.Count 1 "certificate ownership lookup $($Certificate.role)"
    $entry = @($record[0].stores | Where-Object { $_.path -eq $Store })
    Assert-Equal $entry.Count 1 "certificate ownership store lookup $($Certificate.role) $Store"
    return $entry[0]
}

function Save-CertificateOwnership {
    if (-not $PreserveTrustedCertificates) {
        $certificateOwnership | ConvertTo-Json -Depth 8 |
            Set-Content -LiteralPath $ownershipPath -Encoding utf8NoBOM
    }
}

function Ensure-TestCertificatesTrusted {
    foreach ($certificate in $testCertificates) {
        $trustedPath = "Cert:\LocalMachine\TrustedPeople\$($certificate.thumbprint)"
        if (-not (Test-Path -LiteralPath $trustedPath)) {
            Import-Certificate -FilePath $certificate.path -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
            if (-not $PreserveTrustedCertificates) {
                (Get-CertificateOwnershipStore $certificate 'Cert:\LocalMachine\TrustedPeople').introducedByRun = $true
            }
        }
        Assert-True (Test-Path -LiteralPath $trustedPath) "machine trust $($certificate.role) $($certificate.thumbprint)"
    }
    Save-CertificateOwnership
}

function Restore-TestCertificates {
    if ($PreserveTrustedCertificates) {
        return
    }
    foreach ($certificate in $testCertificates) {
        foreach ($store in $certificateStores) {
            $entry = Get-CertificateOwnershipStore $certificate $store
            if ($entry.introducedByRun) {
                $path = "$store\$($certificate.thumbprint)"
                if (Test-Path -LiteralPath $path) {
                    Remove-Item -LiteralPath $path -Force
                }
            }
        }
    }
}

function Assert-TestCertificateState {
    if ($PreserveTrustedCertificates) {
        return @(
            foreach ($certificate in $testCertificates) {
                [ordered]@{
                    role = $certificate.role
                    thumbprint = $certificate.thumbprint
                    preservedByCaller = $true
                }
            }
        )
    }
    $state = @(
        foreach ($certificate in $testCertificates) {
            foreach ($store in $certificateStores) {
                $entry = Get-CertificateOwnershipStore $certificate $store
                $actual = Test-Path -LiteralPath "$store\$($certificate.thumbprint)"
                Assert-Equal $actual $entry.preRunPresent "certificate restoration $($certificate.role) $($certificate.thumbprint) $store"
            }
            [ordered]@{
                role = $certificate.role
                thumbprint = $certificate.thumbprint
                restoredToPreRunState = $true
            }
        }
    )
    return $state
}

function Get-ServiceRecord([string]$Name) {
    $service = Get-CimInstance Win32_Service -Filter "Name='$Name'"
    if (-not $service) {
        throw "SCM service is missing: $Name"
    }
    return $service
}

function Assert-SystemDirectoryAcl([string]$Path, [string]$Label) {
    $acl = Get-Acl -LiteralPath $Path
    Assert-Equal $acl.AreAccessRulesProtected $true "$Label DACL protection"
    $rules = @($acl.Access | Where-Object { $_.AccessControlType -eq 'Allow' })
    $allowed = @('NT AUTHORITY\SYSTEM', 'BUILTIN\Administrators')
    foreach ($rule in $rules) {
        Assert-True ($rule.IdentityReference.Value -in $allowed) "$Label access principal $($rule.IdentityReference.Value)"
    }
    $system = @($rules | Where-Object { $_.IdentityReference.Value -eq 'NT AUTHORITY\SYSTEM' })
    $administrators = @($rules | Where-Object { $_.IdentityReference.Value -eq 'BUILTIN\Administrators' })
    Assert-True ($system.Count -eq 1 -and $system[0].FileSystemRights.ToString().Contains('FullControl')) "$Label SYSTEM access"
    Assert-True ($administrators.Count -eq 1 -and $administrators[0].FileSystemRights.ToString().Contains('FullControl')) "$Label Administrators access"
    return [ordered]@{
        protected = $acl.AreAccessRulesProtected
        principals = @($rules | ForEach-Object { $_.IdentityReference.Value })
    }
}

function Assert-RuntimeDirectoryAcl([string]$Path, [string]$ServiceName) {
    $acl = Get-Acl -LiteralPath $Path
    Assert-Equal $acl.AreAccessRulesProtected $true "runtime DACL protection $Path"
    $rules = @($acl.Access | Where-Object { $_.AccessControlType -eq 'Allow' })
    $system = @($rules | Where-Object { $_.IdentityReference.Value -eq 'NT AUTHORITY\SYSTEM' })
    $administrators = @($rules | Where-Object { $_.IdentityReference.Value -eq 'BUILTIN\Administrators' })
    $users = @($rules | Where-Object { $_.IdentityReference.Value -eq 'BUILTIN\Users' })
    $service = @($rules | Where-Object { $_.IdentityReference.Value -eq "NT SERVICE\$ServiceName" })
    $usersExecute = @($users | Where-Object { $_.FileSystemRights.ToString().Contains('ReadAndExecute') })
    Assert-True ($system.Count -ge 1 -and $system[0].FileSystemRights.ToString().Contains('FullControl')) "SYSTEM runtime access"
    Assert-True ($administrators.Count -ge 1 -and $administrators[0].FileSystemRights.ToString().Contains('FullControl')) "Administrators runtime access"
    Assert-True ($usersExecute.Count -ge 1) "Users runtime execute access"
    Assert-True ($service.Count -ge 1 -and $service[0].FileSystemRights.ToString().Contains('ReadAndExecute')) "service runtime execute access"
    Assert-True (-not $service[0].FileSystemRights.ToString().Contains('Write')) "service runtime write exclusion"
    return [ordered]@{
        protected = $acl.AreAccessRulesProtected
        usersRights = @($users | ForEach-Object { $_.FileSystemRights.ToString() })
        serviceRights = $service[0].FileSystemRights.ToString()
    }
}

function Assert-StoreAcl([string]$Path, [string]$ServiceName) {
    $acl = Get-Acl -LiteralPath $Path
    Assert-Equal $acl.AreAccessRulesProtected $true "store DACL protection $Path"
    $rules = @($acl.Access | Where-Object { $_.AccessControlType -eq 'Allow' })
    $allowed = @('NT AUTHORITY\SYSTEM', 'BUILTIN\Administrators', "NT SERVICE\$ServiceName")
    foreach ($rule in $rules) {
        Assert-True ($rule.IdentityReference.Value -in $allowed) "store access principal $($rule.IdentityReference.Value)"
    }
    $service = @($rules | Where-Object { $_.IdentityReference.Value -eq "NT SERVICE\$ServiceName" })
    Assert-True ($service.Count -eq 1 -and $service[0].FileSystemRights.ToString().Contains('FullControl')) "exact service store access"
    return [ordered]@{
        protected = $acl.AreAccessRulesProtected
        principals = @($rules | ForEach-Object { $_.IdentityReference.Value })
    }
}

function Start-Updater {
    $firstBundle = @($metadata.simulatedBundles | Where-Object { $_.name -eq 'PowerToys-0.101' })[0]
    $secondBundle = @($metadata.simulatedBundles | Where-Object { $_.name -eq 'PowerToys-0.110' })[0]
    $firstUpdater = Join-Path $root "artifacts\simulated-bundles\$($firstBundle.updaterFile)"
    $secondUpdater = Join-Path $root "artifacts\simulated-bundles\$($secondBundle.updaterFile)"
    Assert-Equal (Get-FileHash -LiteralPath $firstUpdater -Algorithm SHA256).Hash $metadata.updater.sha256 'first bundle updater hash'
    Assert-Equal (Get-FileHash -LiteralPath $secondUpdater -Algorithm SHA256).Hash $metadata.updater.sha256 'second bundle updater hash'
    foreach ($source in $firstUpdater, $secondUpdater) {
        $signature = Get-AuthenticodeSignature -LiteralPath $source
        Assert-Equal $signature.Status 'Valid' "updater source signature $source"
        Assert-Equal (Get-CertificateSha256 $signature.SignerCertificate) $metadata.trustedSignerSha256 "updater signer pin $source"
        & $controller --bootstrap-install --updater-binary $source --signer-sha256 $metadata.trustedSignerSha256
        if ($LASTEXITCODE -ne 0) {
            throw "Updater bootstrap failed: $source"
        }
    }
}

function Invoke-Provision(
    [string]$Owner,
    [pscustomobject]$Artifact,
    [string]$CrashPhase = '',
    [switch]$AllowPendingRecovery
) {
    if (-not $AllowPendingRecovery) {
        Assert-JournalsAbsent
    }
    $source = Resolve-ReleaseFile $Artifact.file $Artifact.sha256
    $arguments = @(
        '--provision',
        '--owner-sid', $Owner,
        '--runtime-track', [string]$Artifact.track,
        '--runtime-binary', $source
    )
    if ($CrashPhase) {
        $arguments += @('--crash-phase', $CrashPhase)
    }
    & $controller @arguments | Out-Host
    $exitCode = $LASTEXITCODE
    return $exitCode
}

function Invoke-Cleanup(
    [string]$Owner,
    [string]$CrashPhase = '',
    [switch]$AllowPendingRecovery
) {
    if (-not $AllowPendingRecovery) {
        Assert-JournalsAbsent
    }
    $arguments = @('--cleanup', '--owner-sid', $Owner)
    if ($CrashPhase) {
        $arguments += @('--crash-phase', $CrashPhase)
    }
    & $controller @arguments | Out-Host
    return $LASTEXITCODE
}

function Quote-ServiceArgument([string]$Value) {
    Assert-True (-not $Value.Contains('"')) "service argument quoting $Value"
    return '"' + $Value + '"'
}

function Get-ExpectedRuntimeCommand(
    [string]$Owner,
    [pscustomobject]$Artifact,
    [string]$SiblingOwner = ''
) {
    $serviceName = Get-RuntimeServiceName $Owner
    $executable = Join-Path $installRoot "Runtimes\Track$($Artifact.track)\$($Artifact.version)\PtPuvrRuntime.exe"
    $command = (Quote-ServiceArgument $executable) +
        ' --service-name ' + (Quote-ServiceArgument $serviceName) +
        ' --owner-sid ' + (Quote-ServiceArgument $Owner) +
        ' --runtime-track ' + [string]$Artifact.track +
        ' --runtime-version ' + (Quote-ServiceArgument $Artifact.version)
    if ($SiblingOwner) {
        $command += ' --sibling-owner-sid ' + (Quote-ServiceArgument $SiblingOwner)
    }
    return $command
}

function Assert-Runtime(
    [string]$Owner,
    [pscustomobject]$Artifact,
    [string]$ExpectedSiblingOwner = ''
) {
    $serviceName = Get-RuntimeServiceName $Owner
    $service = Get-ServiceRecord $serviceName
    Assert-Equal $service.StartName "NT SERVICE\$serviceName" "runtime account $serviceName"
    Assert-Equal $service.State 'Running' "runtime state $serviceName"
    Assert-True ($service.ProcessId -ne 0) "runtime PID $serviceName"
    $expectedExecutable = Join-Path $installRoot "Runtimes\Track$($Artifact.track)\$($Artifact.version)\PtPuvrRuntime.exe"
    Assert-Equal $service.PathName (Get-ExpectedRuntimeCommand $Owner $Artifact $ExpectedSiblingOwner) "runtime full ImagePath $serviceName"
    $suffix = $serviceName.Substring('PtPuvrRuntime_'.Length)
    $store = Join-Path $storeRoot $suffix
    $evidence = Read-Evidence (Join-Path $store 'evidence.txt')
    Assert-Equal $evidence.ownerSid $Owner "runtime owner $serviceName"
    Assert-Equal $evidence.runtimeTrack $Artifact.track "runtime track $serviceName"
    Assert-Equal $evidence.runtimeVersion $Artifact.version "runtime version $serviceName"
    Assert-Equal $evidence.tokenUserSid $evidence.serviceSid "runtime primary service SID $serviceName"
    Assert-Equal $evidence.serviceSidPresent 'true' "runtime service SID membership $serviceName"
    Assert-Equal $evidence.packageFullNameResult '15700' "runtime package identity result $serviceName"
    Assert-Equal $evidence.packageIdentityPresent 'false' "runtime package identity absence $serviceName"
    Assert-Equal $evidence.selfBinaryWriteProbe 'denied' "runtime self-binary write denial $serviceName"
    if ($ExpectedSiblingOwner) {
        Assert-Equal $evidence.siblingStoreWriteProbe 'denied' "runtime sibling-store write denial $serviceName"
        Assert-Equal $evidence.siblingOwnerSid $ExpectedSiblingOwner "runtime sibling owner $serviceName"
    }
    else {
        Assert-Equal $evidence.siblingStoreWriteProbe 'not-configured' "runtime sibling-store probe absence $serviceName"
        Assert-True (-not $evidence.PSObject.Properties['siblingOwnerSid']) "runtime sibling owner absence $serviceName"
    }
    $runtimeAcl = Assert-RuntimeDirectoryAcl (Split-Path $expectedExecutable -Parent) $serviceName
    $storeAcl = Assert-StoreAcl $store $serviceName
    return [ordered]@{
        ownerSid = $Owner
        serviceName = $serviceName
        processId = [uint32]$service.ProcessId
        runtimeTrack = [uint16]$Artifact.track
        runtimeVersion = $Artifact.version
        imagePath = $service.PathName
        tokenUserSid = $evidence.tokenUserSid
        serviceSid = $evidence.serviceSid
        executablePath = $evidence.executablePath
        storePath = $store
        packageFullNameResult = [uint32]$evidence.packageFullNameResult
        packageIdentityPresent = $evidence.packageIdentityPresent
        runtimeAcl = $runtimeAcl
        storeAcl = $storeAcl
        selfBinaryWriteProbe = $evidence.selfBinaryWriteProbe
        siblingStoreWriteProbe = $evidence.siblingStoreWriteProbe
    }
}

function Assert-OwnerRemoved([string]$Owner) {
    $serviceName = Get-RuntimeServiceName $Owner
    Assert-True (-not (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) "removed runtime service $serviceName"
    $suffix = $serviceName.Substring('PtPuvrRuntime_'.Length)
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $storeRoot $suffix))) "removed runtime store $serviceName"
    & $controller --status --owner-sid $Owner | Out-Host
    Assert-True ($LASTEXITCODE -ne 0) "removed runtime inventory $serviceName"
}

function Assert-NoWindowsAppsOrMsix {
    $services = @(Get-CimInstance Win32_Service | Where-Object { $_.Name -like 'PtPuvr*' })
    foreach ($service in $services) {
        Assert-True ($service.PathName -notmatch '(?i)WindowsApps|\.msix') "SCM path is ordinary PE $($service.Name)"
    }
    $artifactRoots = @($installRoot, $releaseRoot, (Join-Path $root 'artifacts\simulated-bundles'))
    $artifactFiles = @(
        foreach ($artifactRoot in $artifactRoots) {
            if (Test-Path -LiteralPath $artifactRoot) {
                Get-ChildItem -LiteralPath $artifactRoot -Recurse -File
            }
        }
    )
    Assert-Equal (@($artifactFiles | Where-Object { $_.FullName -match '(?i)WindowsApps|\.msix' }).Count) 0 'artifact paths without WindowsApps or MSIX'
    $textArtifacts = @($artifactFiles | Where-Object { $_.Extension -in '.json', '.txt' })
    foreach ($artifact in $textArtifacts) {
        Assert-True (-not (Select-String -LiteralPath $artifact.FullName -Pattern 'WindowsApps|\.msix' -Quiet)) "artifact content without WindowsApps or MSIX $($artifact.Name)"
    }
    return [ordered]@{
        serviceCount = $services.Count
        artifactFileCount = $artifactFiles.Count
    }
}

function Assert-RejectedCandidate(
    [string]$Owner,
    [uint16]$Track,
    [string]$Source,
    [pscustomobject]$ExpectedRunningArtifact,
    [string]$ExpectedSiblingOwner,
    [string]$Label
) {
    & $controller --provision --owner-sid $Owner --runtime-track $Track --runtime-binary $Source
    Assert-True ($LASTEXITCODE -ne 0) "$Label rejection"
    [void](Assert-Runtime $Owner $ExpectedRunningArtifact $ExpectedSiblingOwner)
}

function Restart-UpdaterAfterCrash {
    $service = Get-Service -Name PtPuvrUpdater -ErrorAction Stop
    if ($service.Status -ne 'Running') {
        Start-Service -Name PtPuvrUpdater
    }
    $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
}

function Assert-StopRestartWithIncompletePipeClient {
    $client = [IO.Pipes.NamedPipeClientStream]::new(
        '.',
        'PtPuvrUpdater',
        [IO.Pipes.PipeDirection]::InOut,
        [IO.Pipes.PipeOptions]::None)
    try {
        $client.Connect(5000)
        Assert-True $client.IsConnected 'incomplete updater pipe client connected'
        $before = Get-ServiceRecord 'PtPuvrUpdater'
        Stop-Service -Name PtPuvrUpdater -ErrorAction Stop
        (Get-Service -Name PtPuvrUpdater -ErrorAction Stop).WaitForStatus(
            'Stopped',
            [TimeSpan]::FromSeconds(30))
        Start-Service -Name PtPuvrUpdater -ErrorAction Stop
        (Get-Service -Name PtPuvrUpdater -ErrorAction Stop).WaitForStatus(
            'Running',
            [TimeSpan]::FromSeconds(30))
        $after = Get-ServiceRecord 'PtPuvrUpdater'
        Assert-Equal $after.State 'Running' 'updater restart after incomplete pipe client'
        Assert-True ($after.ProcessId -ne 0) 'updater PID after incomplete pipe client'
        return [ordered]@{
            client = 'connected-without-request'
            stopped = $true
            restarted = $true
            processIdBeforeStop = [uint32]$before.ProcessId
            processIdAfterRestart = [uint32]$after.ProcessId
        }
    }
    finally {
        $client.Dispose()
    }
}

function Assert-StagingClean {
    $staging = Join-Path $installRoot 'Staging'
    if (Test-Path -LiteralPath $staging) {
        Assert-Equal @(Get-ChildItem -LiteralPath $staging -Force).Count 0 'protected staging cleanup'
    }
}

function Assert-JournalsAbsent {
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $storeRoot 'runtime-transaction.txt'))) 'provision journal cleared'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $storeRoot 'runtime-cleanup-transaction.txt'))) 'cleanup journal cleared'
}

function Assert-RuntimeVersionDirectories([string[]]$Expected) {
    $runtimeRoot = Join-Path $installRoot 'Runtimes'
    $actual = @(
        if (Test-Path -LiteralPath $runtimeRoot) {
            Get-ChildItem -LiteralPath $runtimeRoot -Directory | ForEach-Object {
                Get-ChildItem -LiteralPath $_.FullName -Directory | ForEach-Object {
                    "$($_.Parent.Name)\$($_.Name)"
                }
            }
        }
    ) | Sort-Object
    Assert-Equal ($actual -join '|') (($Expected | Sort-Object) -join '|') 'referenced runtime version directories'
    foreach ($relativePath in $Expected) {
        Assert-True (
            Test-Path -LiteralPath (Join-Path $runtimeRoot (Join-Path $relativePath 'PtPuvrRuntime.exe'))
        ) "runtime executable $relativePath"
    }
}

function Assert-RecoveryState(
    [string]$Label,
    [pscustomobject]$FirstArtifact,
    [string]$FirstSibling,
    [pscustomobject]$SecondArtifact,
    [string]$SecondSibling,
    [string[]]$ExpectedDirectories
) {
    $result = [ordered]@{
        name = $Label
        label = $Label
    }
    if ($FirstArtifact) {
        $result.first = Assert-Runtime $FirstOwnerSid $FirstArtifact $FirstSibling
    }
    else {
        Assert-OwnerRemoved $FirstOwnerSid
    }
    if ($SecondArtifact) {
        $result.second = Assert-Runtime $SecondOwnerSid $SecondArtifact $SecondSibling
    }
    else {
        Assert-OwnerRemoved $SecondOwnerSid
    }
    Assert-JournalsAbsent
    Assert-StagingClean
    Assert-RuntimeVersionDirectories $ExpectedDirectories
    $result.journals = 'absent'
    $result.staging = 'empty'
    $result.versionDirectories = $ExpectedDirectories
    return $result
}

function Assert-OwnerInventoryLimit([pscustomobject]$Artifact) {
    $additionalOwners = @(1200..1229 | ForEach-Object {
        "S-1-5-21-1959867211-618815089-525172305-$_"
    })
    foreach ($owner in $additionalOwners) {
        Assert-Equal (Invoke-Provision $owner $Artifact) 0 "inventory-limit filler provision $owner"
    }
    $inventoryPath = Join-Path $storeRoot 'runtime-inventory.txt'
    Assert-Equal (@(Get-Content -LiteralPath $inventoryPath | Where-Object { $_ }).Count) 32 'inventory owner count at limit'

    $rejectedOwner = 'S-1-5-21-1959867211-618815089-525172305-1230'
    Assert-True ((Invoke-Provision $rejectedOwner $Artifact) -ne 0) 'inventory owner limit rejection'
    $rejectedService = Get-RuntimeServiceName $rejectedOwner
    $rejectedStore = Join-Path $storeRoot $rejectedService.Substring('PtPuvrRuntime_'.Length)
    Assert-True (-not (Get-Service -Name $rejectedService -ErrorAction SilentlyContinue)) 'inventory owner limit service non-mutation'
    Assert-True (-not (Test-Path -LiteralPath $rejectedStore)) 'inventory owner limit store non-mutation'
    Assert-Equal (@(Get-Content -LiteralPath $inventoryPath | Where-Object { $_ }).Count) 32 'inventory owner count after rejection'
    Assert-JournalsAbsent
    Assert-StagingClean
    Assert-RuntimeVersionDirectories @('Track1\1.6.0.0', 'Track1\1.7.0.0', 'Track2\2.0.0.0')

    foreach ($owner in $additionalOwners) {
        Assert-Equal (Invoke-Cleanup $owner) 0 "inventory-limit filler cleanup $owner"
    }
    Assert-Equal (@(Get-Content -LiteralPath $inventoryPath | Where-Object { $_ }).Count) 2 'inventory owner count after limit cleanup'
    return [ordered]@{
        maximumOwners = 32
        rejectedOwner = $rejectedOwner
        candidateServiceCreated = $false
        candidateStoreCreated = $false
        journals = 'absent'
        staging = 'empty'
    }
}

function Remove-All {
    $updater = Get-Service -Name PtPuvrUpdater -ErrorAction SilentlyContinue
    if ($updater -and $updater.Status -ne 'Running') {
        try {
            Start-Service -Name PtPuvrUpdater
            $updater.WaitForStatus('Running', [TimeSpan]::FromSeconds(20))
        }
        catch {
            Write-Warning "Updater could not be started for managed cleanup: $($_.Exception.Message)"
        }
    }
    foreach ($owner in $FirstOwnerSid, $SecondOwnerSid) {
        if ((Get-Service -Name PtPuvrUpdater -ErrorAction SilentlyContinue).Status -eq 'Running') {
            & $controller --cleanup --owner-sid $owner
        }
        $serviceName = Get-RuntimeServiceName $owner
        $runtime = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
        if ($runtime) {
            if ($runtime.Status -ne 'Stopped') {
                Stop-Service -Name $serviceName -Force
                $runtime.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
            }
            sc.exe delete $serviceName | Out-Null
        }
    }
    foreach ($runtime in @(Get-Service -Name 'PtPuvrRuntime_*' -ErrorAction SilentlyContinue)) {
        if ($runtime.Status -ne 'Stopped') {
            Stop-Service -Name $runtime.Name -Force
            $runtime.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }
        sc.exe delete $runtime.Name | Out-Null
    }
    $updater = Get-Service -Name PtPuvrUpdater -ErrorAction SilentlyContinue
    if ($updater) {
        if ($updater.Status -ne 'Stopped') {
            Stop-Service -Name PtPuvrUpdater -Force
            $updater.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }
        sc.exe delete PtPuvrUpdater | Out-Null
    }
    Remove-Item -LiteralPath $installRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $storeRoot -Recurse -Force -ErrorAction SilentlyContinue
    Restore-TestCertificates
}

function Assert-Teardown {
    Assert-Equal @(Get-Service -Name 'PtPuvr*' -ErrorAction SilentlyContinue).Count 0 'remaining prototype services'
    Assert-True (-not (Test-Path -LiteralPath $installRoot)) 'remaining protected install root'
    Assert-True (-not (Test-Path -LiteralPath $storeRoot)) 'remaining protected store root'
    return [ordered]@{
        services = 0
        installRootPresent = $false
        storeRootPresent = $false
        certificates = Assert-TestCertificateState
    }
}

function Show-Status {
    foreach ($owner in $FirstOwnerSid, $SecondOwnerSid) {
        & $controller --status --owner-sid $owner
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime status failed: $owner"
        }
    }
}

function Provision-Two {
    $first = Get-RuntimeArtifact 'track1-1.0.0.0'
    $second = Get-RuntimeArtifact 'track2-2.0.0.0'
    Assert-Equal (Invoke-Provision $FirstOwnerSid $first) 0 'first runtime provision'
    Assert-Equal (Invoke-Provision $SecondOwnerSid $second) 0 'second runtime provision'
}

function Invoke-Validation {
    $events = [System.Collections.Generic.List[object]]::new()
    $validation = [ordered]@{
        timestamp = (Get-Date).ToString('o')
        topology = [ordered]@{
            packageIdentityRequired = $false
            updaterService = 'PtPuvrUpdater'
            runtimeRoot = $installRoot
            storeRoot = $storeRoot
            trustedBootstrapAssumption = 'controller simulates a trusted installer bootstrap'
            callerAuthorization = 'administrators-only mechanism; production authorization remains open'
        }
        events = $events
        verdict = 'FAIL'
    }
    $failure = $null
    try {
        Ensure-TestCertificatesTrusted
        Start-Updater
        $updater = Get-ServiceRecord 'PtPuvrUpdater'
        $updaterPath = Join-Path $installRoot 'Updater\5.0.0.0\PtPuvrUpdater.exe'
        Assert-Equal $updater.StartName 'LocalSystem' 'updater account'
        Assert-Equal $updater.State 'Running' 'updater state'
        Assert-True ($updater.ProcessId -ne 0) 'updater PID'
        Assert-Equal $updater.PathName ('"' + $updaterPath + '"') 'updater protected ImagePath'
        $updaterEvidence = Read-Evidence (Join-Path $storeRoot 'updater-evidence.txt')
        Assert-Equal $updaterEvidence.tokenUserSid 'S-1-5-18' 'updater token SID'
        Assert-Equal $updaterEvidence.packageFullNameResult '15700' 'updater package identity result'
        Assert-Equal $updaterEvidence.packageIdentityPresent 'false' 'updater package identity absence'
        Assert-Equal $updaterEvidence.updaterVersion '5.0.0.0' 'updater version'
        Assert-Equal $updaterEvidence.bootstrapTrustAssumption 'trusted-installer-simulation' 'bootstrap assumption evidence'
        Assert-Equal $updaterEvidence.trustedSignerSha256 $metadata.trustedSignerSha256 'updater trusted signer pin evidence'
        Assert-Equal (Get-Content -LiteralPath (Join-Path $storeRoot 'trusted-signer-sha256.txt') -Raw) $metadata.trustedSignerSha256 'protected signer pin policy'
        $updaterSignature = Get-AuthenticodeSignature -LiteralPath $updaterPath
        Assert-Equal $updaterSignature.Status 'Valid' 'installed updater signature'
        Assert-Equal (Get-CertificateSha256 $updaterSignature.SignerCertificate) $metadata.trustedSignerSha256 'installed updater signer pin'
        $updaterAcl = Assert-SystemDirectoryAcl (Split-Path $updaterPath -Parent) 'updater install directory'
        $stateRootAcl = Assert-SystemDirectoryAcl $storeRoot 'updater state root'
        $events.Add([ordered]@{
            name = 'bootstrap'
            updaterPid = [uint32]$updater.ProcessId
            account = $updater.StartName
            executable = $updaterPath
            packageFullNameResult = [uint32]$updaterEvidence.packageFullNameResult
            packageIdentityPresent = $updaterEvidence.packageIdentityPresent
            trustedSignerSha256 = $updaterEvidence.trustedSignerSha256
            updaterAcl = $updaterAcl
            stateRootAcl = $stateRootAcl
        })

        $v100 = Get-RuntimeArtifact 'track1-1.0.0.0'
        $v110 = Get-RuntimeArtifact 'track1-1.1.0.0'
        $v120 = Get-RuntimeArtifact 'track1-1.2.0.0'
        $v130 = Get-RuntimeArtifact 'track1-1.3.0.0'
        $v140 = Get-RuntimeArtifact 'track1-1.4.0.0'
        $v150 = Get-RuntimeArtifact 'track1-1.5.0.0'
        $v160 = Get-RuntimeArtifact 'track1-1.6.0.0'
        $v170 = Get-RuntimeArtifact 'track1-1.7.0.0'
        $v180 = Get-RuntimeArtifact 'track1-1.8.0.0'
        $v200 = Get-RuntimeArtifact 'track2-2.0.0.0'
        $events.Add([ordered]@{
            name = 'incomplete-pipe-client-stop-restart'
            evidence = Assert-StopRestartWithIncompletePipeClient
        })
        Assert-Equal (Invoke-Provision $FirstOwnerSid $v100) 0 'track 1 initial provision'
        Assert-Equal (Invoke-Provision $SecondOwnerSid $v200) 0 'track 2 initial provision'
        $firstInitial = Assert-Runtime $FirstOwnerSid $v100 $SecondOwnerSid
        $secondInitial = Assert-Runtime $SecondOwnerSid $v200 $FirstOwnerSid
        Assert-True ($firstInitial.processId -ne $secondInitial.processId) 'distinct runtime processes'
        Assert-True ($firstInitial.serviceSid -ne $secondInitial.serviceSid) 'distinct exact service SIDs'
        $events.Add([ordered]@{ name = 'concurrent-distinct-owners'; runtimes = @($firstInitial, $secondInitial) })

        $tampered = Resolve-ReleaseFile $metadata.negativeCandidates.tampered.file
        Assert-RejectedCandidate $FirstOwnerSid 1 $tampered $v100 $SecondOwnerSid 'tampered candidate'
        $wrongProduct = Resolve-ReleaseFile $metadata.negativeCandidates.wrongProduct.file
        Assert-RejectedCandidate $FirstOwnerSid 1 $wrongProduct $v100 $SecondOwnerSid 'wrong-product candidate'
        $foreignSigner = Resolve-ReleaseFile $metadata.negativeCandidates.foreignSigner.file
        Assert-RejectedCandidate $FirstOwnerSid 1 $foreignSigner $v100 $SecondOwnerSid 'foreign trusted signer candidate'
        Assert-RejectedCandidate $FirstOwnerSid 1 (Resolve-ReleaseFile $v200.file $v200.sha256) $v100 $SecondOwnerSid 'wrong-track candidate'
        $events.Add([ordered]@{
            name = 'candidate-rejections'
            tampered = 'rejected'
            wrongProduct = 'rejected'
            foreignTrustedSigner = 'rejected-after-machine-trust'
            wrongTrack = 'rejected'
            preservedVersion = '1.0.0.0'
        })

        Assert-Equal (Invoke-Provision $FirstOwnerSid $v110) 0 'track 1 successful upgrade'
        [void](Assert-Runtime $FirstOwnerSid $v110 $SecondOwnerSid)
        $events.Add([ordered]@{ name = 'upgrade'; from = '1.0.0.0'; to = '1.1.0.0'; result = 'running' })

        Assert-RejectedCandidate $FirstOwnerSid 1 (Resolve-ReleaseFile $v100.file $v100.sha256) $v110 $SecondOwnerSid 'downgrade candidate'
        $events.Add([ordered]@{ name = 'downgrade'; candidate = '1.0.0.0'; result = 'rejected'; preservedVersion = '1.1.0.0' })

        Assert-True ((Invoke-Provision $FirstOwnerSid $v120) -ne 0) 'intentional readiness failure'
        [void](Assert-Runtime $FirstOwnerSid $v110 $SecondOwnerSid)
        $events.Add([ordered]@{ name = 'readiness-rollback'; candidate = '1.2.0.0'; result = 'rolled-back'; preservedVersion = '1.1.0.0' })

        Assert-True ((Invoke-Provision $FirstOwnerSid $v130 'after-journal-prepared') -ne 0) 'journal-prepared crash injection'
        Restart-UpdaterAfterCrash
        $events.Add((Assert-RecoveryState `
            'crash-after-journal-prepared' $v110 $SecondOwnerSid $v200 $FirstOwnerSid `
            @('Track1\1.1.0.0', 'Track2\2.0.0.0')))

        Assert-Equal (Invoke-Provision $FirstOwnerSid $v130) 0 'journal crash retry'
        [void](Assert-Runtime $FirstOwnerSid $v130 $SecondOwnerSid)
        $events.Add([ordered]@{ name = 'retry-after-journal-crash'; result = '1.3.0.0' })

        Assert-True ((Invoke-Provision $FirstOwnerSid $v140 'after-final-install') -ne 0) 'final install crash injection'
        Restart-UpdaterAfterCrash
        $events.Add((Assert-RecoveryState `
            'crash-after-final-install' $v130 $SecondOwnerSid $v200 $FirstOwnerSid `
            @('Track1\1.3.0.0', 'Track2\2.0.0.0')))

        Assert-Equal (Invoke-Provision $FirstOwnerSid $v140) 0 'final install crash retry'
        [void](Assert-Runtime $FirstOwnerSid $v140 $SecondOwnerSid)
        $events.Add([ordered]@{ name = 'retry-after-final-install-crash'; result = '1.4.0.0' })

        Assert-True ((Invoke-Provision $FirstOwnerSid $v150 'after-scm-repath') -ne 0) 'SCM repath crash injection'
        Restart-UpdaterAfterCrash
        $events.Add((Assert-RecoveryState `
            'crash-after-scm-repath' $v140 $SecondOwnerSid $v200 $FirstOwnerSid `
            @('Track1\1.4.0.0', 'Track2\2.0.0.0')))

        Assert-Equal (Invoke-Provision $FirstOwnerSid $v150) 0 'SCM repath crash retry'
        [void](Assert-Runtime $FirstOwnerSid $v150 $SecondOwnerSid)
        $events.Add([ordered]@{ name = 'retry-after-scm-repath-crash'; result = '1.5.0.0' })

        Assert-Equal (Invoke-Cleanup $FirstOwnerSid) 0 'pre-inventory-crash topology cleanup'
        $withoutFirst = Assert-RecoveryState `
            'topology-before-inventory-crash' $null '' $v200 '' @('Track2\2.0.0.0')
        $secondBeforeSynchronization = $withoutFirst.second.imagePath
        Assert-True ((Invoke-Provision $FirstOwnerSid $v160 'after-inventory-before-sync') -ne 0) 'inventory commit crash injection'
        Restart-UpdaterAfterCrash
        $afterInventoryRecovery = Assert-RecoveryState `
            'crash-after-inventory-before-sync' $v160 $SecondOwnerSid $v200 $FirstOwnerSid `
            @('Track1\1.6.0.0', 'Track2\2.0.0.0')
        Assert-True (
            $afterInventoryRecovery.second.imagePath -ne $secondBeforeSynchronization
        ) 'inventory recovery synchronized retained sibling topology'
        $afterInventoryRecovery.synchronization = 'retained sibling changed from no sibling argument to exact first-owner argument'
        $events.Add($afterInventoryRecovery)

        Assert-True ((Invoke-Provision $FirstOwnerSid $v170 'after-unreferenced-runtime-delete') -ne 0) 'unreferenced runtime deletion crash injection'
        Restart-UpdaterAfterCrash
        $events.Add((Assert-RecoveryState `
            'crash-after-unreferenced-runtime-delete' $v170 $SecondOwnerSid $v200 $FirstOwnerSid `
            @('Track1\1.7.0.0', 'Track2\2.0.0.0')))

        Assert-True (
            (Invoke-Provision $FirstOwnerSid $v180 'after-target-directory-created') -ne 0
        ) 'target directory creation crash injection'
        $incompleteTarget = Join-Path $installRoot 'Runtimes\Track1\1.8.0.0'
        Assert-True (Test-Path -LiteralPath $incompleteTarget -PathType Container) 'incomplete target directory present'
        $removeAllRemnant = Join-Path $incompleteTarget 'remove-all-remnant'
        New-Item -ItemType Directory -Path $removeAllRemnant -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $removeAllRemnant 'partial.bin') -Value 'incomplete'
        Restart-UpdaterAfterCrash
        $targetDirectoryRecovery = Assert-RecoveryState `
            'crash-after-target-directory-created' $v170 $SecondOwnerSid $v200 $FirstOwnerSid `
            @('Track1\1.7.0.0', 'Track2\2.0.0.0')
        Assert-True (-not (Test-Path -LiteralPath $incompleteTarget)) 'incomplete unreferenced target removal'
        Assert-Equal (Invoke-Provision $FirstOwnerSid $v180) 0 'target directory crash retry'
        [void](Assert-Runtime $FirstOwnerSid $v180 $SecondOwnerSid)
        $targetDirectoryRecovery.remnant = 'incomplete unreferenced version directory removed without deleting referenced roots'
        $targetDirectoryRecovery.retry = '1.8.0.0 accepted'
        $events.Add($targetDirectoryRecovery)

        Assert-True (
            (Invoke-Cleanup $FirstOwnerSid 'fail-after-cleanup-service-delete') -ne 0
        ) 'ordinary cleanup failure injection'
        Assert-Equal (Get-ServiceRecord 'PtPuvrUpdater').State 'Running' 'updater remains available after ordinary cleanup failure'
        Assert-True (
            Test-Path -LiteralPath (Join-Path $storeRoot 'runtime-cleanup-transaction.txt')
        ) 'ordinary cleanup failure leaves recoverable cleanup journal'
        Assert-Equal (
            (Invoke-Provision $FirstOwnerSid $v170 -AllowPendingRecovery)
        ) 0 'later provision recovers cleanup journal before mutation'
        $ordinaryCleanupRecovery = Assert-RecoveryState `
            'ordinary-cleanup-failure-gated-before-later-provision' $v170 $SecondOwnerSid $v200 $FirstOwnerSid `
            @('Track1\1.7.0.0', 'Track2\2.0.0.0')
        $ordinaryCleanupRecovery.convergence = 'later provision began only after cleanup journal roll-forward'
        $events.Add($ordinaryCleanupRecovery)

        Assert-True ((Invoke-Cleanup $FirstOwnerSid 'after-cleanup-service-delete') -ne 0) 'cleanup service deletion crash injection'
        Restart-UpdaterAfterCrash
        $events.Add((Assert-RecoveryState `
            'crash-after-cleanup-service-delete' $null '' $v200 '' @('Track2\2.0.0.0')))

        Assert-Equal (Invoke-Provision $FirstOwnerSid $v160) 0 'reprovision after service deletion cleanup crash'
        [void](Assert-Runtime $FirstOwnerSid $v160 $SecondOwnerSid)

        Assert-True ((Invoke-Cleanup $SecondOwnerSid 'after-cleanup-inventory') -ne 0) 'cleanup inventory removal crash injection'
        Restart-UpdaterAfterCrash
        $events.Add((Assert-RecoveryState `
            'crash-after-cleanup-inventory' $v160 '' $null '' @('Track1\1.6.0.0')))

        Assert-Equal (Invoke-Provision $SecondOwnerSid $v200) 0 'reprovision after inventory cleanup crash'
        $finalFirst = Assert-Runtime $FirstOwnerSid $v160 $SecondOwnerSid
        $finalSecond = Assert-Runtime $SecondOwnerSid $v200 $FirstOwnerSid
        $ownerLimit = Assert-OwnerInventoryLimit $v170
        $finalState = Assert-RecoveryState `
            'final-running-state' $v160 $SecondOwnerSid $v200 $FirstOwnerSid `
            @('Track1\1.6.0.0', 'Track2\2.0.0.0')
        $ordinaryArtifactPolicy = Assert-NoWindowsAppsOrMsix
        $events.Add([ordered]@{
            name = 'owner-limit'
            evidence = $ownerLimit
        })
        $events.Add([ordered]@{
            name = 'final-running-state'
            runtimes = @($finalFirst, $finalSecond)
            recovery = $finalState
            ordinaryArtifactPolicy = $ordinaryArtifactPolicy
        })
    }
    catch {
        $failure = $_
    }

    try {
        Remove-All
        $validation.teardown = Assert-Teardown
    }
    catch {
        if (-not $failure) {
            $failure = $_
        }
        $validation.teardown = [ordered]@{ error = $_.Exception.Message }
    }
    if (-not $failure) {
        $validation.verdict = 'PASS'
    }
    else {
        $validation.failure = $failure.Exception.Message
    }
    $validation | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resultPath -Encoding utf8NoBOM
    if ($failure) {
        throw $failure
    }
    Write-Host "VALIDATION PASS: $resultPath"
}

$testCertificates = @(Get-TestCertificates)
$certificateOwnership = Get-CertificateOwnership $testCertificates

switch ($Verb) {
    'bootstrap' { Start-Updater }
    'provision-two' { Provision-Two }
    'status' { Show-Status }
    'cleanup' { Remove-All }
    'validate' { Invoke-Validation }
}
