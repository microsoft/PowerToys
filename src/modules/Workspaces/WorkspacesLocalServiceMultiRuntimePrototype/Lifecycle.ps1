[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$releaseRoot = Join-Path $root 'artifacts\release'
$releaseSetsRoot = Join-Path $root 'artifacts\release-sets'
$metadataPath = Join-Path $releaseRoot 'artifacts.json'
$ownershipPath = Join-Path $releaseRoot 'certificate-ownership.json'
$installRoot = Join-Path $env:ProgramFiles 'PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype'
$storeRoot = Join-Path $env:ProgramData 'Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype'
$endpointRegistryPath = 'HKLM:\SOFTWARE\Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype'
$packageName = 'Microsoft.PowerToys.WsPuvr.ControlPlane'
$hostServiceName = 'PtPuvrHost'
$createdUsers = [Collections.Generic.List[object]]::new()

function Assert-True($Value, [string]$Label) {
    if (-not $Value) {
        throw "Assertion failed: $Label"
    }
}

function Assert-Equal($Actual, $Expected, [string]$Label) {
    if ($Actual -ne $Expected) {
        throw "Assertion failed: $Label; expected='$Expected', actual='$Actual'"
    }
}

function Test-Elevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Convert-ExitCodeToUInt32([int]$ExitCode) {
    return [BitConverter]::ToUInt32([BitConverter]::GetBytes($ExitCode), 0)
}

function New-PrototypePassword {
    $bytes = New-Object byte[] 18
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return 'Puvr!' + [Convert]::ToHexString($bytes) + 'a1'
}

function New-TestUser([string]$Name) {
    if (Get-LocalUser -Name $Name -ErrorAction SilentlyContinue) {
        throw "Refusing to reuse pre-existing local user: $Name"
    }
    $password = New-PrototypePassword
    $securePassword = ConvertTo-SecureString $password -AsPlainText -Force
    $user = New-LocalUser `
        -Name $Name `
        -Password $securePassword `
        -FullName "Packaged control-plane prototype $Name" `
        -PasswordNeverExpires `
        -UserMayNotChangePassword
    $context = [pscustomobject]@{
        name = $Name
        sid = $user.SID.Value
        credential = [PSCredential]::new("$env:COMPUTERNAME\$Name", $securePassword)
        profile = $null
        layout = $null
        client = $null
    }
    Assert-True (-not @(
        Get-LocalGroupMember -Group 'Administrators' |
            Where-Object SID -eq $context.sid
    )) "standard user is not administrator: $Name"
    $createdUsers.Add($context)
    return $context
}

function Get-ProfilePathForSid([string]$Sid) {
    $key = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$Sid"
    return [Environment]::ExpandEnvironmentVariables(
        (Get-ItemProperty -LiteralPath $key -ErrorAction Stop).ProfileImagePath)
}

function Initialize-UserLayout([object]$User) {
    $profileProcess = Start-Process `
        -FilePath "$env:SystemRoot\System32\cmd.exe" `
        -ArgumentList '/c exit 0' `
        -Credential $User.credential `
        -LoadUserProfile `
        -WorkingDirectory "$env:SystemRoot\System32" `
        -Wait `
        -PassThru
    Assert-Equal $profileProcess.ExitCode 0 "profile initialization: $($User.name)"

    $User.profile = Get-ProfilePathForSid $User.sid
    $User.layout = Join-Path $User.profile (
        'AppData\Local\Microsoft\PowerToys\WorkspacesControlPlanePrototype')
    $inbox = Join-Path $User.layout 'ReleaseInbox'
    New-Item -ItemType Directory -Path $inbox -Force | Out-Null
    foreach ($releaseId in 'release-102') {
        Copy-Item `
            -LiteralPath (Join-Path $releaseSetsRoot $releaseId) `
            -Destination (Join-Path $inbox $releaseId) `
            -Recurse `
            -Force
    }
    $User.client = Join-Path $User.layout 'PtPuvrClientHarness.exe'
    Copy-Item `
        -LiteralPath (Join-Path $releaseRoot 'PtPuvrClientHarness.exe') `
        -Destination $User.client `
        -Force
    $acl = Get-Acl -LiteralPath $User.layout
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner([Security.Principal.SecurityIdentifier]$User.sid)
    foreach ($sid in 'S-1-5-18', 'S-1-5-32-544', $User.sid) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new(
            [Security.Principal.SecurityIdentifier]$sid,
            [Security.AccessControl.FileSystemRights]::FullControl,
            [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit',
            [Security.AccessControl.PropagationFlags]::None,
            [Security.AccessControl.AccessControlType]::Allow)
        [void]$acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $User.layout -AclObject $acl
    & icacls.exe $User.layout `
        /inheritance:r `
        /grant:r `
        '*S-1-5-18:(OI)(CI)F' `
        '*S-1-5-32-544:(OI)(CI)F' `
        "*$($User.sid):(OI)(CI)F" `
        /T `
        /C | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not secure test-user layout: $($User.name)"
    }
    & icacls.exe $User.layout /setowner "*$($User.sid)" /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not set test-user layout owner: $($User.name)"
    }
}

function Invoke-UserClient(
    [object]$User,
    [string[]]$Arguments,
    [uint32]$ExpectedWin32 = 0
) {
    $suffix = [Guid]::NewGuid().ToString('N')
    $stdout = Join-Path $root "artifacts\packaged-$suffix.stdout.txt"
    $stderr = Join-Path $root "artifacts\packaged-$suffix.stderr.txt"
    $process = Start-Process `
        -FilePath $User.client `
        -ArgumentList $Arguments `
        -Credential $User.credential `
        -LoadUserProfile `
        -WorkingDirectory $User.layout `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -Wait `
        -PassThru
    $output = if (Test-Path -LiteralPath $stdout) {
        Get-Content -LiteralPath $stdout -Raw
    } else { '' }
    $errors = if (Test-Path -LiteralPath $stderr) {
        Get-Content -LiteralPath $stderr -Raw
    } else { '' }
    $actualExit = Convert-ExitCodeToUInt32 $process.ExitCode
    if ($actualExit -ne $ExpectedWin32) {
        throw "Client exit mismatch for $($User.name) $($Arguments -join ' '): " +
            "expected=$ExpectedWin32 actual=$actualExit stdout='$output' stderr='$errors'"
    }
    $values = [ordered]@{}
    foreach ($line in ($output -split '\r?\n')) {
        $separator = $line.IndexOf('=')
        if ($separator -gt 0) {
            $values[$line.Substring(0, $separator)] =
                $line.Substring($separator + 1)
        }
    }
    if (-not $values.Contains('win32')) {
        throw "Client emitted no reply: stdout='$output'; stderr='$errors'"
    }
    Assert-Equal ([uint32]$values.win32) $ExpectedWin32 'client protocol status'
    Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
    return [pscustomobject]$values
}

function Get-PackageArtifact([string]$Version) {
    $packages = @($metadata.updaterPackages |
        Where-Object packageVersion -eq $Version)
    Assert-Equal $packages.Count 1 "one Updater package artifact: $Version"
    Assert-True (Test-Path -LiteralPath $packages[0].path -PathType Leaf) `
        "Updater package exists: $Version"
    return $packages[0]
}

function Get-ProvisionedPackage {
    return Get-AppxProvisionedPackage -Online |
        Where-Object DisplayName -eq $packageName |
        Select-Object -First 1
}

function Wait-HostService([int]$Seconds = 30) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        $service = Get-CimInstance Win32_Service `
            -Filter "Name='$hostServiceName'" `
            -ErrorAction SilentlyContinue
        if ($service) {
            return $service
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    throw 'Packaged Host service registration did not appear.'
}

function Install-MachinePackage([object]$Artifact, [switch]$ForceRollback) {
    if ($ForceRollback) {
        $previous = Get-ProvisionedPackage
        Assert-True $previous 'machine provisioning exists before rollback'
        Add-AppxPackage `
            -Path $Artifact.path `
            -ForceUpdateFromAnyVersion `
            -ForceApplicationShutdown `
            -ErrorAction Stop
        $remaining = Get-ProvisionedPackage
        if ($remaining -and $remaining.PackageName -eq $previous.PackageName) {
            Remove-AppxProvisionedPackage `
                -Online `
                -PackageName $previous.PackageName `
                -ErrorAction Stop | Out-Null
            $remaining = $null
        }
        if (-not $remaining) {
            Add-AppxProvisionedPackage `
                -Online `
                -PackagePath $Artifact.path `
                -SkipLicense `
                -ErrorAction Stop | Out-Null
        }
    }
    else {
        Add-AppxProvisionedPackage `
            -Online `
            -PackagePath $Artifact.path `
            -SkipLicense `
            -ErrorAction Stop | Out-Null
    }
    $provisioned = Get-ProvisionedPackage
    Assert-True $provisioned "machine provisioning exists: $($Artifact.packageVersion)"
    Assert-True (
        $provisioned.PackageName -like
            "$packageName`_$($Artifact.packageVersion)_x64__*"
    ) "machine provisioning version: $($Artifact.packageVersion)"
    return Wait-HostService
}

function Start-PackagedHost {
    $startupError = Join-Path $storeRoot 'host-startup-error.txt'
    Remove-Item -LiteralPath $startupError -Force -ErrorAction SilentlyContinue
    $deadline = (Get-Date).AddSeconds(30)
    do {
        try {
            Start-Service -Name $hostServiceName -ErrorAction Stop
            break
        }
        catch {
            if ((Get-Date) -ge $deadline) {
                throw
            }
            # Package registration and prior session-0 package activation can
            # briefly leave AppModel returning ERROR_INVALID_PARAMETER.
            Start-Sleep -Milliseconds 500
        }
    } while ($true)
    $service = Get-Service -Name $hostServiceName
    $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
    $cim = Get-CimInstance Win32_Service -Filter "Name='$hostServiceName'"
    Assert-Equal $cim.StartName 'LocalSystem' 'packaged Host account'
    Assert-True (
        $cim.PathName -like
            '"C:\Program Files\WindowsApps\Microsoft.PowerToys.WsPuvr.ControlPlane_*\PtPuvrHost.exe"'
    ) 'packaged Host WindowsApps ImagePath'
    return $cim
}

function Read-KeyValueFile([string]$Path) {
    Assert-True (Test-Path -LiteralPath $Path -PathType Leaf) "evidence exists: $Path"
    $values = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $separator = $line.IndexOf('=')
        if ($separator -gt 0) {
            $values[$line.Substring(0, $separator)] =
                $line.Substring($separator + 1)
        }
    }
    return [pscustomobject]$values
}

function Assert-HostEvidence([string]$Version) {
    $evidence = Read-KeyValueFile (Join-Path $storeRoot 'host-evidence.txt')
    Assert-Equal $evidence.tokenUserSid 'S-1-5-18' 'Host LocalSystem token'
    Assert-Equal $evidence.bootstrapOrigin `
        'machine-provisioned-signed-msix' 'Host bootstrap origin'
    Assert-Equal $evidence.hostSelfServicing `
        'appx-package-deployment' 'Host self-servicing boundary'
    Assert-Equal $evidence.packageIdentityPresent 'true' 'Host package identity'
    Assert-Equal $evidence.packageVersion $Version 'Host package version'
    Assert-True (
        $evidence.executablePath -like
            'C:\Program Files\WindowsApps\Microsoft.PowerToys.WsPuvr.ControlPlane_*\PtPuvrHost.exe'
    ) 'Host evidence executable path'
    return $evidence
}

function Get-RuntimeServices {
    return @(
        Get-CimInstance Win32_Service |
            Where-Object Name -like 'PtPuvrRuntime_*'
    )
}

function Get-ProtectedLeaseCount {
    $leasePath = Join-Path $storeRoot 'leases.txt'
    Assert-True (Test-Path -LiteralPath $leasePath -PathType Leaf) `
        'protected lease state exists'
    return @(
        Get-Content -LiteralPath $leasePath |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    ).Count
}

function Assert-RuntimeTopology([object[]]$Users, [object[]]$Before) {
    $services = @(Get-RuntimeServices)
    Assert-Equal $services.Count 2 'two ordinary per-SID Runtime services'
    foreach ($service in $services) {
        Assert-Equal $service.State 'Running' "Runtime running: $($service.Name)"
        Assert-True (
            $service.PathName -like
                '"C:\Program Files\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype\Runtimes\*\PtPuvrRuntime.exe"*'
        ) "Runtime ordinary protected PE ImagePath: $($service.Name)"
        Assert-True (
            $service.StartName -eq "NT SERVICE\$($service.Name)"
        ) "Runtime virtual-account identity: $($service.Name)"
        if ($Before.Count -gt 0) {
            $prior = @($Before | Where-Object Name -eq $service.Name)
            Assert-Equal $prior.Count 1 "Runtime existed before package servicing: $($service.Name)"
            Assert-Equal $service.ProcessId $prior[0].ProcessId `
                "Runtime process survived package servicing: $($service.Name)"
        }
    }

    $evidenceFiles = @(
        Get-ChildItem -LiteralPath $storeRoot -Filter evidence.txt -Recurse -File |
            Where-Object FullName -notlike '*\Requests\*'
    )
    Assert-Equal $evidenceFiles.Count 2 'two Runtime evidence files'
    $ownerSids = @()
    foreach ($file in $evidenceFiles) {
        $evidence = Read-KeyValueFile $file.FullName
        Assert-Equal $evidence.packageIdentityPresent 'false' `
            "ordinary Runtime has no package identity: $($evidence.serviceName)"
        Assert-Equal $evidence.readiness 'ready' "Runtime readiness: $($evidence.serviceName)"
        Assert-True (
            $evidence.executablePath -like
                'C:\Program Files\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype\Runtimes\*\PtPuvrRuntime.exe'
        ) "Runtime evidence ordinary path: $($evidence.serviceName)"
        $ownerSids += $evidence.ownerSid
    }
    foreach ($user in $Users) {
        Assert-True ($user.sid -in $ownerSids) "Runtime owner evidence: $($user.name)"
    }
    return $services
}

function Stop-PackagedHost {
    $service = Get-Service -Name $hostServiceName -ErrorAction SilentlyContinue
    if ($service -and $service.Status -ne 'Stopped') {
        Stop-Service -Name $hostServiceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
}

function Get-HostExecutableFromService {
    $service = Get-CimInstance Win32_Service `
        -Filter "Name='$hostServiceName'" `
        -ErrorAction SilentlyContinue
    if (-not $service) {
        return $null
    }
    return $service.PathName.Trim('"')
}

function Invoke-HostMaintenance([string]$Verb) {
    $hostPath = Get-HostExecutableFromService
    Assert-True ($hostPath -and (Test-Path -LiteralPath $hostPath -PathType Leaf)) `
        "packaged Host is available for $Verb"
    $process = Start-Process `
        -FilePath $hostPath `
        -ArgumentList $Verb `
        -Wait `
        -PassThru
    Assert-Equal (Convert-ExitCodeToUInt32 $process.ExitCode) 0 `
        "Host maintenance result: $Verb"
}

function Remove-TestUser([object]$User) {
    Remove-LocalUser -Name $User.name -ErrorAction SilentlyContinue
    $profile = Get-CimInstance Win32_UserProfile `
        -Filter "SID='$($User.sid)'" `
        -ErrorAction SilentlyContinue
    if ($profile) {
        $profile | Remove-CimInstance -ErrorAction SilentlyContinue
    }
}

function Remove-PackageBestEffort {
    $provisioned = Get-ProvisionedPackage
    if ($provisioned) {
        Remove-AppxProvisionedPackage `
            -Online `
            -PackageName $provisioned.PackageName `
            -AllUsers `
            -ErrorAction SilentlyContinue | Out-Null
    }
    foreach ($package in @(Get-AppxPackage -AllUsers -Name $packageName -ErrorAction SilentlyContinue)) {
        Remove-AppxPackage `
            -Package $package.PackageFullName `
            -AllUsers `
            -ErrorAction SilentlyContinue
    }
}

function Restore-OwnedCertificates {
    foreach ($record in @($ownership.certificates)) {
        foreach ($store in @($record.stores)) {
            if (-not $store.preRunPresent) {
                Get-ChildItem -Path $store.path |
                    Where-Object Thumbprint -eq $record.thumbprint |
                    Remove-Item -Force
            }
        }
    }
}

if (-not (Test-Elevated)) {
    throw 'Packaged lifecycle validation requires an elevated administrator token.'
}
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $ownershipPath -PathType Leaf)) {
    throw 'Run Package.ps1 -TrustMachine before Lifecycle.ps1.'
}

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$ownership = Get-Content -LiteralPath $ownershipPath -Raw | ConvertFrom-Json
Assert-Equal $metadata.format 3 'artifact metadata format'
Assert-Equal $ownership.format 2 'certificate ownership format'
$packageV5 = Get-PackageArtifact '5.0.0.0'
$packageV6 = Get-PackageArtifact '6.0.0.0'
$users = @()
$completed = $false
$result = $null

try {
    Assert-True (-not (Get-ProvisionedPackage)) 'no pre-existing machine provisioning'
    Assert-True (-not (Get-Service -Name $hostServiceName -ErrorAction SilentlyContinue)) `
        'no pre-existing packaged Host service'
    Assert-Equal @(Get-RuntimeServices).Count 0 'no pre-existing Runtime services'
    Assert-True (-not (Test-Path -LiteralPath $installRoot)) 'no pre-existing install root'
    Assert-True (-not (Test-Path -LiteralPath $storeRoot)) 'no pre-existing store root'

    [void](Install-MachinePackage $packageV5)
    $hostV5 = Start-PackagedHost
    $evidenceV5 = Assert-HostEvidence '5.0.0.0'
    Assert-True (
        Test-Path -LiteralPath (
            Join-Path $installRoot 'Engines\5.0.0.0\PtPuvrUpdater.exe'
        ) -PathType Leaf
    ) 'packaged Host seeded the initial ordinary Engine'
    foreach ($bootstrapFile in
        'code-signer-sha256.txt',
        'metadata-signer-sha256.txt',
        'Policy\PtPuvrCodePolicy.exe',
        'Policy\PtPuvrMetadataPolicy.exe') {
        Assert-True (
            Test-Path -LiteralPath (Join-Path $storeRoot $bootstrapFile) -PathType Leaf
        ) "packaged Host seeded protected bootstrap file: $bootstrapFile"
    }

    $users = @(
        New-TestUser 'PtPuvrOwnerA'
        New-TestUser 'PtPuvrOwnerB'
    )
    foreach ($user in $users) {
        Initialize-UserLayout $user
        $reply = Invoke-UserClient $user @('--acquire', '--release-id', 'release-102')
        Assert-Equal $reply.activeEngineVersion '5.1.0.0' `
            "release activated Engine for $($user.name)"
    }
    $runtimeBeforeUpdate = @(Assert-RuntimeTopology $users @())
    $statusBeforeUpdate = Invoke-UserClient $users[0] @('--status')
    Assert-Equal $statusBeforeUpdate.leaseCount '1' 'caller lease before package update'
    Assert-Equal (Get-ProtectedLeaseCount) 2 'two protected leases before package update'
    Assert-Equal $statusBeforeUpdate.activeEngineVersion '5.1.0.0' `
        'active ordinary Engine before package update'

    $runtimeSnapshot = @($runtimeBeforeUpdate | ForEach-Object {
        [pscustomobject]@{ Name = $_.Name; ProcessId = $_.ProcessId }
    })
    [void](Install-MachinePackage $packageV6)
    $hostAfterUpdate = Get-CimInstance Win32_Service -Filter "Name='$hostServiceName'"
    Assert-Equal $hostAfterUpdate.State 'Stopped' 'AppX update stopped packaged Host'
    Assert-True (
        $hostAfterUpdate.PathName -like
            '"C:\Program Files\WindowsApps\Microsoft.PowerToys.WsPuvr.ControlPlane_6.0.0.0_*\PtPuvrHost.exe"'
    ) 'AppX update repathed packaged Host to v6'
    [void](Assert-RuntimeTopology $users $runtimeSnapshot)
    [void](Start-PackagedHost)
    [void](Assert-HostEvidence '6.0.0.0')
    $statusAfterUpdate = Invoke-UserClient $users[1] @('--status')
    Assert-Equal $statusAfterUpdate.leaseCount '1' 'caller lease survived package update'
    Assert-Equal (Get-ProtectedLeaseCount) 2 'two leases survived package update'
    Assert-Equal $statusAfterUpdate.activeEngineVersion '5.1.0.0' `
        'ordinary Engine state survived package update'

    [void](Install-MachinePackage $packageV5 -ForceRollback)
    $hostAfterRollback = Get-CimInstance Win32_Service -Filter "Name='$hostServiceName'"
    Assert-Equal $hostAfterRollback.State 'Stopped' 'AppX rollback stopped packaged Host'
    Assert-True (
        $hostAfterRollback.PathName -like
            '"C:\Program Files\WindowsApps\Microsoft.PowerToys.WsPuvr.ControlPlane_5.0.0.0_*\PtPuvrHost.exe"'
    ) 'AppX rollback repathed packaged Host to v5'
    [void](Assert-RuntimeTopology $users $runtimeSnapshot)
    [void](Start-PackagedHost)
    [void](Assert-HostEvidence '5.0.0.0')
    $statusAfterRollback = Invoke-UserClient $users[0] @('--status')
    Assert-Equal $statusAfterRollback.leaseCount '1' 'caller lease survived package rollback'
    Assert-Equal (Get-ProtectedLeaseCount) 2 'two leases survived package rollback'
    Assert-Equal $statusAfterRollback.activeEngineVersion '5.1.0.0' `
        'ordinary Engine state survived package rollback'

    foreach ($user in $users) {
        [void](Invoke-UserClient $user @('--release'))
    }
    Assert-Equal @(Get-RuntimeServices).Count 0 'Runtime services removed after lease release'
    Stop-PackagedHost
    Invoke-HostMaintenance '--package-uninstall-check'
    Invoke-HostMaintenance '--package-uninstall-cleanup'
    Remove-PackageBestEffort
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline -and
        (Get-Service -Name $hostServiceName -ErrorAction SilentlyContinue)) {
        Start-Sleep -Milliseconds 250
    }
    Assert-True (-not (Get-ProvisionedPackage)) 'machine provisioning removed'
    Assert-True (-not (Get-Service -Name $hostServiceName -ErrorAction SilentlyContinue)) `
        'packaged Host service removed'
    Assert-True (-not (Test-Path -LiteralPath $installRoot)) 'ordinary install root removed'
    Assert-True (-not (Test-Path -LiteralPath $storeRoot)) 'protected store root removed'
    Assert-True (-not (Test-Path -LiteralPath $endpointRegistryPath)) `
        'endpoint registry key removed'

    $result = [ordered]@{
        timestamp = (Get-Date).ToString('o')
        verdict = 'PASS'
        updater = [ordered]@{
            delivery = 'machine-provisioned signed MSIX'
            service = $hostServiceName
            account = 'LocalSystem'
            versionsValidated = @('5.0.0.0', '6.0.0.0', '5.0.0.0 rollback')
            packageIdentity = $true
        }
        ordinaryRuntime = [ordered]@{
            owners = @($users.sid)
            services = @($runtimeSnapshot.Name)
            survivedUpdaterUpdateAndRollback = $true
            packageIdentity = $false
        }
        protectedState = [ordered]@{
            activeEngineVersion = '5.1.0.0'
            survivedUpdaterUpdateAndRollback = $true
        }
        extraProductionExecutables = 0
    }
    $result |
        ConvertTo-Json -Depth 8 |
        Set-Content `
            -LiteralPath (Join-Path $root 'artifacts\packaged-lifecycle-result.json') `
            -Encoding utf8NoBOM
    $completed = $true
}
finally {
    if (-not $completed) {
        try {
            if ((Get-Service -Name $hostServiceName -ErrorAction SilentlyContinue).Status -eq
                'Running') {
                foreach ($user in $users) {
                    try { [void](Invoke-UserClient $user @('--release')) } catch {}
                }
            }
        } catch {}
        try { Stop-PackagedHost } catch {}
        try {
            if (Get-HostExecutableFromService) {
                Invoke-HostMaintenance '--package-uninstall-cleanup'
            }
        } catch {}
        foreach ($runtime in @(Get-RuntimeServices)) {
            & sc.exe stop $runtime.Name | Out-Null
            & sc.exe delete $runtime.Name | Out-Null
        }
        Remove-PackageBestEffort
        Remove-Item -LiteralPath $installRoot, $storeRoot -Recurse -Force `
            -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $endpointRegistryPath -Recurse -Force `
            -ErrorAction SilentlyContinue
    }
    foreach ($user in $createdUsers) {
        Remove-TestUser $user
    }
    Restore-OwnedCertificates
}

$result | ConvertTo-Json -Depth 8
Write-Output 'PACKAGED UPDATER + ORDINARY RUNTIME LIFECYCLE PASS'
