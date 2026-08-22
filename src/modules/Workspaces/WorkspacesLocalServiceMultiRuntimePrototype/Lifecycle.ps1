[CmdletBinding()]
param(
    [ValidateSet('validate', 'cleanup', 'status')]
    [string]$Verb = 'validate',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core' -or $PSVersionTable.PSVersion -lt [version]'7.0') {
    throw 'Lifecycle.ps1 requires PowerShell 7 or later (pwsh.exe).'
}
$root = $PSScriptRoot
$releaseRoot = Join-Path $root 'artifacts\release'
$releaseSetsRoot = Join-Path $root 'artifacts\release-sets'
$metadataPath = Join-Path $releaseRoot 'artifacts.json'
$resultPath = Join-Path $root 'artifacts\validation-result.json'
$installRoot = Join-Path $env:ProgramFiles 'PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype'
$storeRoot = Join-Path $env:ProgramData 'Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype'
$hostPath = Join-Path $installRoot 'PtPuvrHost.exe'
$clientPath = Join-Path $installRoot 'PtPuvrUserClient.exe'
$endpointRegistryPath = 'HKLM:\SOFTWARE\Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype'
$cleanupOutcomeRegistryPath = 'HKLM:\SOFTWARE\Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototypeValidation'
$msiPath = Join-Path $root 'artifacts\msi\PtPuvrControlPlane.msi'
$ownerNames = @('PtPuvrOwnerA', 'PtPuvrOwnerB')
$certificateStores = @(
    'Cert:\CurrentUser\My',
    'Cert:\CurrentUser\TrustedPeople',
    'Cert:\LocalMachine\TrustedPeople'
)

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Lifecycle operations require an elevated administrator token.'
}
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $msiPath -PathType Leaf)) {
    throw 'Run Package.ps1 before Lifecycle.ps1.'
}

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$ownershipPath = Join-Path $releaseRoot 'certificate-ownership.json'
$ownership = Get-Content -LiteralPath $ownershipPath -Raw | ConvertFrom-Json
if ($ownership.format -ne 2) {
    throw 'Certificate ownership state format is unsupported.'
}
$createdUsers = [System.Collections.Generic.List[object]]::new()

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

function Get-CertificateSha256([Security.Cryptography.X509Certificates.X509Certificate2]$Certificate) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Certificate.RawData))
}

function Get-CertificateEntries([string]$Store, [string]$Thumbprint) {
    return @(
        Get-ChildItem -Path $Store | Where-Object { $_.Thumbprint -eq $Thumbprint }
    )
}

function Remove-ExactCertificateEntries([string]$Store, [string]$Thumbprint) {
    Get-CertificateEntries $Store $Thumbprint | ForEach-Object {
        Remove-Item -LiteralPath $_.PSPath -Force
    }
}

function Get-Certificates {
    $definitions = @(
        [ordered]@{ role = 'code'; property = 'codeSigner' }
        [ordered]@{ role = 'metadata'; property = 'metadataSigner' }
        [ordered]@{ role = 'foreign'; property = 'foreignSigner' }
    )
    return @(
        foreach ($definition in $definitions) {
            $record = $metadata.($definition.property)
            $path = Join-Path $releaseRoot $record.certificateFile
            $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($path)
            Assert-Equal $certificate.Thumbprint $record.thumbprint "certificate thumbprint $($definition.role)"
            Assert-Equal (Get-CertificateSha256 $certificate) $record.signerSha256 "certificate pin $($definition.role)"
            [pscustomobject]@{
                role = $definition.role
                path = $path
                thumbprint = $record.thumbprint
                signerSha256 = $record.signerSha256
            }
        }
    )
}

function Get-OwnershipRecord([string]$Role) {
    $record = @($ownership.certificates | Where-Object { $_.role -eq $Role })
    Assert-Equal $record.Count 1 "certificate ownership role $Role"
    return $record[0]
}

function Ensure-CertificatesTrusted([object[]]$Certificates) {
    foreach ($certificate in $Certificates) {
        $record = Get-OwnershipRecord $certificate.role
        $storeRecord = @($record.stores | Where-Object { $_.path -eq 'Cert:\LocalMachine\TrustedPeople' })
        Assert-Equal $storeRecord.Count 1 "certificate ownership machine trust $($certificate.role)"
        if (@(Get-CertificateEntries $storeRecord[0].path $certificate.thumbprint).Count -eq 0) {
            $storeRecord[0].introducedByRun = $true
            $ownership | ConvertTo-Json -Depth 8 |
                Set-Content -LiteralPath $ownershipPath -Encoding utf8NoBOM
            Import-Certificate -FilePath $certificate.path -CertStoreLocation $storeRecord[0].path | Out-Null
        }
        Assert-True (
            @(Get-CertificateEntries $storeRecord[0].path $certificate.thumbprint).Count -ge 1
        ) "machine trust $($certificate.role)"
    }
}

function Restore-Certificates([object[]]$Certificates) {
    foreach ($certificate in $Certificates) {
        $record = Get-OwnershipRecord $certificate.role
        foreach ($store in $record.stores) {
            if ($store.introducedByRun) {
                Remove-ExactCertificateEntries $store.path $certificate.thumbprint
            }
            $actual = @(Get-CertificateEntries $store.path $certificate.thumbprint).Count -ge 1
            Assert-Equal $actual $store.preRunPresent "certificate restoration $($certificate.role) $($store.path)"
        }
    }
}

function Get-RuntimeServiceName([string]$OwnerSid) {
    $bytes = [Text.Encoding]::Unicode.GetBytes($OwnerSid)
    $digest = [Security.Cryptography.SHA256]::HashData($bytes)
    return 'PtPuvrRuntime_' + [Convert]::ToHexString($digest).ToLowerInvariant().Substring(0, 16)
}

function Read-Evidence([string]$Path) {
    Assert-True (Test-Path -LiteralPath $Path -PathType Leaf) "evidence exists $Path"
    $values = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $separator = $line.IndexOf('=')
        if ($separator -gt 0) {
            $values[$line.Substring(0, $separator)] = $line.Substring($separator + 1)
        }
    }
    return [pscustomobject]$values
}

function Get-ServiceRecord([string]$Name) {
    $service = Get-CimInstance Win32_Service -Filter "Name='$Name'"
    Assert-True $service "SCM service $Name"
    return $service
}

function Get-HostActiveEngine {
    return (Get-Content -LiteralPath (Join-Path $storeRoot 'active-engine.txt') -Raw)
}

function Get-PublishedHostEndpoint {
    $key = Get-ItemProperty -LiteralPath $endpointRegistryPath -ErrorAction Stop
    $endpoint = [string]$key.HostEndpoint
    Assert-True (
        $endpoint -match '^\\\\\.\\pipe\\PtPuvrHost-[0-9a-f]{32}$'
    ) 'registry publishes a bounded random host endpoint'
    return $endpoint
}

function Assert-Host([string]$ExpectedEngine = '5.0.0.0') {
    $service = Get-ServiceRecord 'PtPuvrHost'
    Assert-Equal $service.State 'Running' 'host running'
    Assert-Equal $service.StartName 'LocalSystem' 'host LocalSystem'
    Assert-Equal $service.PathName ('"' + $hostPath + '"') 'stable host SCM ImagePath'
    $evidence = Read-Evidence (Join-Path $storeRoot 'host-evidence.txt')
    Assert-Equal $evidence.bootstrapOrigin 'companion-msi' 'host MSI origin evidence'
    Assert-Equal $evidence.pipePolicy 'random-128bit-first-instance-anchor-system-secondary-pool-au-data-rw-no-create-instance-raw-dos-image-provisional-sid-quota-preface-token-match-timeout-5000ms-reject-remote' 'host pipe policy'
    $endpoint = Get-PublishedHostEndpoint
    Assert-Equal $evidence.pipeEndpoint $endpoint 'host evidence matches registry endpoint'
    Assert-Equal $evidence.hostSelfServicing 'msi-or-external-repair-only' 'host self-service boundary'
    Assert-Equal $evidence.childProcessPolicy 'kill-on-close-job-stop-aware-120000ms' 'host child process policy'
    Assert-Equal $evidence.activeEngineVersion $ExpectedEngine 'host active engine evidence'
    Assert-Equal (Get-HostActiveEngine) $ExpectedEngine 'active engine state'
    return [ordered]@{
        processId = [uint32]$service.ProcessId
        imagePath = $service.PathName
        activeEngine = $ExpectedEngine
        codeSignerSha256 = $evidence.codeSignerSha256
        metadataSignerSha256 = $evidence.metadataSignerSha256
        pipePolicy = $evidence.pipePolicy
        pipeListenerCount = [uint32]$evidence.pipeListenerCount
        pipePerSidActiveConnectionLimit = [uint32]$evidence.pipePerSidActiveConnectionLimit
        pipeEndpoint = $endpoint
    }
}

function Assert-Runtime(
    [object]$Owner,
    [string]$ExpectedVersion,
    [string]$ExpectedSiblingStoreProbe = 'denied'
) {
    $serviceName = Get-RuntimeServiceName $Owner.sid
    $service = Get-ServiceRecord $serviceName
    Assert-Equal $service.State 'Running' "runtime state $($Owner.name)"
    Assert-Equal $service.StartName "NT SERVICE\$serviceName" "runtime virtual account $($Owner.name)"
    $suffix = $serviceName.Substring('PtPuvrRuntime_'.Length)
    $evidence = Read-Evidence (Join-Path $storeRoot "$suffix\evidence.txt")
    Assert-Equal $evidence.ownerSid $Owner.sid "runtime derived owner SID $($Owner.name)"
    Assert-Equal $evidence.runtimeVersion $ExpectedVersion "runtime version $($Owner.name)"
    Assert-Equal $evidence.tokenUserSid $evidence.serviceSid "runtime virtual-account SID $($Owner.name)"
    Assert-Equal $evidence.packageIdentityPresent 'false' "ordinary runtime identity $($Owner.name)"
    Assert-Equal $evidence.selfBinaryWriteProbe 'denied' "runtime self binary write protection $($Owner.name)"
    Assert-Equal `
        $evidence.siblingStoreWriteProbe `
        $ExpectedSiblingStoreProbe `
        "runtime sibling store protection $($Owner.name)"
    return [ordered]@{
        ownerSid = $Owner.sid
        serviceName = $serviceName
        processId = [uint32]$service.ProcessId
        serviceSid = $evidence.serviceSid
        store = Join-Path $storeRoot $suffix
        runtimeVersion = $evidence.runtimeVersion
    }
}

function Assert-RuntimeRemoved([object]$Owner) {
    $serviceName = Get-RuntimeServiceName $Owner.sid
    Assert-True (-not (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) "runtime removed $($Owner.name)"
    $suffix = $serviceName.Substring('PtPuvrRuntime_'.Length)
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $storeRoot $suffix))) "runtime store removed $($Owner.name)"
}

function Invoke-Msi(
    [ValidateSet('install', 'uninstall', 'repair', 'remove-feature')]$Action,
    [string]$LogName
) {
    $arguments = switch ($Action) {
        'install' { "/i `"$msiPath`"" }
        'uninstall' { "/x `"$msiPath`"" }
        'repair' { "/fa `"$msiPath`"" }
        'remove-feature' { "/i `"$msiPath`" REMOVE=ControlPlaneFeature" }
    }
    $logPath = Join-Path (Join-Path $root 'artifacts\msi') $LogName
    $argumentList = "$arguments /qn /norestart /l*v `"$logPath`""
    $process = Start-Process -FilePath msiexec.exe -ArgumentList $argumentList -Wait -PassThru
    return [pscustomobject]@{ exitCode = $process.ExitCode; log = $logPath }
}

function Get-ExactPrototypeTombstones([string]$Path) {
    $parent = Split-Path -Parent $Path
    $leaf = Split-Path -Leaf $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        return @()
    }
    $escapedLeaf = [regex]::Escape($leaf)
    return @(
        Get-ChildItem -LiteralPath $parent -Force -Directory |
            Where-Object {
                $_.Name -match "^$escapedLeaf\.PtPuvrDelete-[0-9a-f]{32}$"
            }
    )
}

function Remove-ExactRegistryKey([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    Assert-True (-not (Test-Path -LiteralPath $Path)) "registry key removed: $Path"
}

function Clear-CleanupOutcome {
    Remove-ExactRegistryKey $cleanupOutcomeRegistryPath
}

function Invoke-RawMsiUninstallAndAssertCommitCleanup {
    Clear-CleanupOutcome
    [int64]$startedFileTimeUtc = [DateTime]::UtcNow.ToFileTimeUtc()
    $uninstall = Invoke-Msi uninstall 'raw-uninstall-commit-cleanup.log'
    Assert-Equal $uninstall.exitCode 0 'raw MSI uninstall exit code'

    Assert-True (Test-Path -LiteralPath $cleanupOutcomeRegistryPath) `
        'commit cleanup wrote a durable outcome outside deleted roots'
    $outcome = Get-ItemProperty -LiteralPath $cleanupOutcomeRegistryPath
    Assert-True (
        [string]$outcome.CleanupRunNonce -match '^[0-9a-f]{32}$'
    ) 'commit cleanup outcome has an exact run nonce'
    Assert-Equal ([uint32]$outcome.CleanupWin32Status) 0 `
        'commit cleanup durable Win32 outcome'
    Assert-Equal ([string]$outcome.CleanupStage) 'complete' `
        'commit cleanup durable stage'
    [int64]$outcomeFileTime = $outcome.CleanupTimestampFileTimeUtc
    Assert-True (
        $outcomeFileTime -ge $startedFileTimeUtc -and
        $outcomeFileTime -le [DateTime]::UtcNow.AddMinutes(1).ToFileTimeUtc()
    ) 'commit cleanup outcome timestamp belongs to this raw uninstall'
    $outcomeAcl = Get-Acl -LiteralPath $cleanupOutcomeRegistryPath
    Assert-True $outcomeAcl.AreAccessRulesProtected `
        'commit cleanup outcome registry DACL is protected'

    Assert-True (-not (Test-Path -LiteralPath $installRoot)) `
        'raw MSI commit cleanup removed the exact Program Files root'
    Assert-Equal @(Get-ExactPrototypeTombstones $installRoot).Count 0 `
        'raw MSI commit cleanup removed all exact Program Files tombstones'
    Assert-True (-not (Test-Path -LiteralPath $storeRoot)) `
        'raw MSI commit cleanup removed the exact ProgramData root'
    Assert-Equal @(Get-ExactPrototypeTombstones $storeRoot).Count 0 `
        'raw MSI commit cleanup removed all exact ProgramData tombstones'
    Assert-True (-not (Test-Path -LiteralPath $endpointRegistryPath)) `
        'raw MSI commit cleanup removed endpoint publication and key'
    return [ordered]@{
        msiExitCode = [uint32]$uninstall.exitCode
        log = $uninstall.log
        nonce = [string]$outcome.CleanupRunNonce
        timestampFileTimeUtc = $outcomeFileTime
        win32Status = [uint32]$outcome.CleanupWin32Status
        stage = [string]$outcome.CleanupStage
        installRootPresent = $false
        installTombstones = 0
        storeRootPresent = $false
        storeTombstones = 0
        endpointRegistryPresent = $false
        fallbackUsedBeforeAssertions = $false
    }
}

function Assert-MsiRegistered {
    $entries = @(
        Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue
    ) | Where-Object {
        $displayName = $_.PSObject.Properties['DisplayName']
        $null -ne $displayName -and $displayName.Value -eq $metadata.msi.productName
    }
    Assert-True ($entries.Count -ge 1) 'MSI product registration'
    return $entries[0]
}

function Restart-HostAfterCrash {
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $service = Get-Service -Name 'PtPuvrHost' -ErrorAction SilentlyContinue
        if ($service -and $service.Status -eq 'Running') {
            return
        }
        if ($service -and $service.Status -eq 'Stopped') {
            Start-Service -Name 'PtPuvrHost'
            $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
            return
        }
        Start-Sleep -Milliseconds 250
    }
    throw 'Host did not restart after deterministic crash.'
}

function Assert-PartialStateFailsClosed {
    $leasePath = Join-Path $storeRoot 'leases.txt'
    $heldPath = Join-Path $storeRoot 'leases.partial-state-test'
    $before = Get-ProtectedStateSnapshot
    Stop-Service -Name PtPuvrHost
    (Get-Service -Name PtPuvrHost).WaitForStatus(
        'Stopped',
        [TimeSpan]::FromSeconds(15))
    Move-Item -LiteralPath $leasePath -Destination $heldPath
    try {
        Start-Service -Name PtPuvrHost -ErrorAction SilentlyContinue
        $deadline = (Get-Date).AddSeconds(4)
        $failed = $null
        while ((Get-Date) -lt $deadline) {
            $candidate = Get-CimInstance Win32_Service -Filter "Name='PtPuvrHost'"
            if ($candidate.State -eq 'Stopped' -and [uint32]$candidate.ExitCode -eq 13) {
                $failed = $candidate
                break
            }
            Start-Sleep -Milliseconds 100
        }
        Assert-True $failed 'partial mutable state fails host startup with ERROR_INVALID_DATA'
        Assert-True (
            -not (Get-ItemProperty -LiteralPath $endpointRegistryPath).PSObject.Properties['HostEndpoint']
        ) 'failed partial-state startup publishes no endpoint'
    }
    finally {
        Move-Item -LiteralPath $heldPath -Destination $leasePath -Force
    }
    (Get-Service -Name PtPuvrHost).WaitForStatus(
        'Running',
        [TimeSpan]::FromSeconds(15))
    Assert-Equal (Get-ProtectedStateSnapshot) $before `
        'partial-state startup attempt does not reset mutable state'
    [void](Assert-Host '5.0.0.0')
    return [ordered]@{
        failedWin32 = 13
        endpointPublishedOnFailure = $false
        nonCrashFailureRestarted = $true
        mutableStateReset = $false
    }
}

function Assert-MutableStateReplacementRecovery {
    $stateNames = @(
        'active-engine.txt',
        'engine-version-floor.txt',
        'runtime-version-floor-track1.txt',
        'accepted-release-state.txt',
        'leases.txt',
        'runtime-inventory.txt'
    )
    $before = Get-ProtectedStateSnapshot

    Stop-Service -Name PtPuvrHost
    (Get-Service -Name PtPuvrHost).WaitForStatus(
        'Stopped',
        [TimeSpan]::FromSeconds(15))
    foreach ($name in $stateNames) {
        $path = Join-Path $storeRoot $name
        Copy-Item -LiteralPath $path -Destination "$path.new"
    }
    Start-Service -Name PtPuvrHost
    (Get-Service -Name PtPuvrHost).WaitForStatus(
        'Running',
        [TimeSpan]::FromSeconds(30))
    foreach ($name in $stateNames) {
        Assert-True (
            -not (Test-Path -LiteralPath ((Join-Path $storeRoot $name) + '.new'))
        ) "primary-authoritative stale replacement removed: $name"
    }
    Assert-Equal (Get-ProtectedStateSnapshot) $before `
        'primary-authoritative mutable replacement recovery preserves exact state'
    [void](Assert-Host '5.0.0.0')

    Stop-Service -Name PtPuvrHost
    (Get-Service -Name PtPuvrHost).WaitForStatus(
        'Stopped',
        [TimeSpan]::FromSeconds(15))
    foreach ($name in $stateNames) {
        $path = Join-Path $storeRoot $name
        Move-Item -LiteralPath $path -Destination "$path.new"
    }
    Start-Service -Name PtPuvrHost
    (Get-Service -Name PtPuvrHost).WaitForStatus(
        'Running',
        [TimeSpan]::FromSeconds(30))
    foreach ($name in $stateNames) {
        $path = Join-Path $storeRoot $name
        Assert-True (Test-Path -LiteralPath $path -PathType Leaf) `
            "safe only replacement promoted: $name"
        Assert-True (-not (Test-Path -LiteralPath "$path.new")) `
            "safe only replacement consumed: $name"
    }
    Assert-Equal (Get-ProtectedStateSnapshot) $before `
        'safe only-replacement recovery preserves exact state'
    [void](Assert-Host '5.0.0.0')

    $leasePath = Join-Path $storeRoot 'leases.txt'
    Stop-Service -Name PtPuvrHost
    (Get-Service -Name PtPuvrHost).WaitForStatus(
        'Stopped',
        [TimeSpan]::FromSeconds(15))
    Set-Content `
        -LiteralPath "$leasePath.new" `
        -Value 'not-a-canonical-sid' `
        -Encoding utf8NoBOM `
        -NoNewline
    try {
        Start-Service -Name PtPuvrHost -ErrorAction SilentlyContinue
        $deadline = (Get-Date).AddSeconds(6)
        $failed = $null
        while ((Get-Date) -lt $deadline) {
            $candidate = Get-CimInstance Win32_Service -Filter "Name='PtPuvrHost'"
            if ($candidate.State -eq 'Stopped' -and [uint32]$candidate.ExitCode -eq 13) {
                $failed = $candidate
                break
            }
            Start-Sleep -Milliseconds 100
        }
        Assert-True $failed 'malformed mutable replacement fails closed with ERROR_INVALID_DATA'
    }
    finally {
        Remove-Item -LiteralPath "$leasePath.new" -Force -ErrorAction SilentlyContinue
    }
    (Get-Service -Name PtPuvrHost).WaitForStatus(
        'Running',
        [TimeSpan]::FromSeconds(30))
    Assert-Equal (Get-ProtectedStateSnapshot) $before `
        'malformed replacement failure preserves exact authoritative state'
    [void](Assert-Host '5.0.0.0')

    return [ordered]@{
        files = $stateNames
        primaryPlusNew = 'validated-primary-and-removed-stale-new'
        onlyNew = 'validated-and-promoted'
        malformedNew = 'ERROR_INVALID_DATA'
        exactStatePreserved = $true
        hostRestarted = $true
    }
}

if (-not ('PtPuvrLifecycleNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public static class PtPuvrLifecycleNative
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateFile(
        string name,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateNamedPipe(
        string name,
        uint openMode,
        uint pipeMode,
        uint maximumInstances,
        uint outBufferSize,
        uint inBufferSize,
        uint defaultTimeout,
        IntPtr securityAttributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadFile(
        IntPtr handle,
        byte[] buffer,
        uint bytesToRead,
        out uint bytesRead,
        IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetNamedPipeInfo(
        IntPtr handle,
        out uint flags,
        out uint outBufferSize,
        out uint inBufferSize,
        out uint maximumInstances);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct TRUSTEE
    {
        public IntPtr pMultipleTrustee;
        public int MultipleTrusteeOperation;
        public int TrusteeForm;
        public int TrusteeType;
        public IntPtr ptstrName;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern uint GetSecurityInfo(
        IntPtr handle,
        int objectType,
        uint securityInfo,
        out IntPtr owner,
        out IntPtr group,
        out IntPtr dacl,
        out IntPtr sacl,
        out IntPtr securityDescriptor);

    [DllImport("advapi32.dll")]
    public static extern void BuildTrusteeWithSid(
        ref TRUSTEE trustee,
        IntPtr sid);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern uint GetEffectiveRightsFromAcl(
        IntPtr acl,
        ref TRUSTEE trustee,
        out uint accessRights);

    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr memory);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    public static extern uint MsiOpenDatabase(
        string databasePath,
        IntPtr persist,
        out IntPtr database);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    public static extern uint MsiDatabaseOpenView(
        IntPtr database,
        string query,
        out IntPtr view);

    [DllImport("msi.dll")]
    public static extern uint MsiViewExecute(IntPtr view, IntPtr record);

    [DllImport("msi.dll")]
    public static extern uint MsiViewFetch(IntPtr view, out IntPtr record);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    public static extern uint MsiRecordGetString(
        IntPtr record,
        uint field,
        StringBuilder value,
        ref uint valueCharacters);

    [DllImport("msi.dll")]
    public static extern uint MsiCloseHandle(IntPtr handle);
}
'@
}

function Get-MsiFileRows {
    $database = [IntPtr]::Zero
    $view = [IntPtr]::Zero
    try {
        $status = [PtPuvrLifecycleNative]::MsiOpenDatabase(
            $msiPath,
            [IntPtr]::Zero,
            [ref]$database)
        Assert-Equal $status 0 'open MSI database'
        $status = [PtPuvrLifecycleNative]::MsiDatabaseOpenView(
            $database,
            'SELECT `FileName` FROM `File`',
            [ref]$view)
        Assert-Equal $status 0 'open MSI File-table view'
        Assert-Equal (
            [PtPuvrLifecycleNative]::MsiViewExecute($view, [IntPtr]::Zero)
        ) 0 'execute MSI File-table view'
        $files = [System.Collections.Generic.List[string]]::new()
        while ($true) {
            $record = [IntPtr]::Zero
            $status = [PtPuvrLifecycleNative]::MsiViewFetch($view, [ref]$record)
            if ($status -eq 259) {
                break
            }
            Assert-Equal $status 0 'fetch MSI File-table row'
            try {
                [uint32]$characters = 512
                $value = [Text.StringBuilder]::new([int]$characters)
                Assert-Equal (
                    [PtPuvrLifecycleNative]::MsiRecordGetString(
                        $record,
                        1,
                        $value,
                        [ref]$characters)
                ) 0 'read MSI File-table value'
                $files.Add($value.ToString().Split('|')[-1])
            }
            finally {
                [void][PtPuvrLifecycleNative]::MsiCloseHandle($record)
            }
        }
        return @($files)
    }
    finally {
        if ($view -ne [IntPtr]::Zero) {
            [void][PtPuvrLifecycleNative]::MsiCloseHandle($view)
        }
        if ($database -ne [IntPtr]::Zero) {
            [void][PtPuvrLifecycleNative]::MsiCloseHandle($database)
        }
    }
}

function Open-ControlPipe([string]$Endpoint = (Get-PublishedHostEndpoint)) {
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline) {
        $handle = [PtPuvrLifecycleNative]::CreateFile(
            $Endpoint,
            0x00120003,
            0,
            [IntPtr]::Zero,
            3,
            0,
            [IntPtr]::Zero)
        if (-not $handle.IsInvalid) {
            return $handle
        }
        $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        if ($errorCode -notin @(2, 231)) {
            throw "CreateFile($Endpoint) failed with Win32 $errorCode."
        }
        $handle.Dispose()
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out opening $Endpoint."
}

function Get-PipeMaximumInstances([Microsoft.Win32.SafeHandles.SafeFileHandle]$Pipe) {
    [uint32]$flags = 0
    [uint32]$outBufferSize = 0
    [uint32]$inBufferSize = 0
    [uint32]$maximumInstances = 0
    Assert-True (
        [PtPuvrLifecycleNative]::GetNamedPipeInfo(
            $Pipe.DangerousGetHandle(),
            [ref]$flags,
            [ref]$outBufferSize,
            [ref]$inBufferSize,
            [ref]$maximumInstances)
    ) 'query external named-pipe instance bound'
    Assert-True ($maximumInstances -gt 1) 'host exposes a multi-instance listener bound'
    return $maximumInstances
}

function Assert-RawPipeRejected(
    [Microsoft.Win32.SafeHandles.SafeFileHandle]$Pipe,
    [string]$Label
) {
    $buffer = New-Object byte[] 1
    [uint32]$read = 0
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $success = [PtPuvrLifecycleNative]::ReadFile(
        $Pipe.DangerousGetHandle(),
        $buffer,
        1,
        [ref]$read,
        [IntPtr]::Zero)
    $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    $watch.Stop()
    Assert-True (-not $success) "$Label direct raw read is rejected"
    Assert-True ($errorCode -in @(5, 109, 233, 995)) "$Label direct raw rejection status"
    Assert-True ($watch.ElapsedMilliseconds -lt 2000) "$Label direct raw rejection is immediate"
    return [uint64]$watch.ElapsedMilliseconds
}

function Assert-FirstPipeInstanceAnchor([string]$Endpoint = (Get-PublishedHostEndpoint)) {
    $competing = [PtPuvrLifecycleNative]::CreateNamedPipe(
        $Endpoint,
        0x00080003,
        0x00000006,
        1,
        4096,
        4096,
        0,
        [IntPtr]::Zero)
    $createError = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    try {
        Assert-True $competing.IsInvalid 'competing first named-pipe instance rejected'
        Assert-Equal $createError 231 `
            'competing first named-pipe instance Win32 status'
    }
    finally {
        $competing.Dispose()
    }
}

function Get-AuthenticatedUsersPipeRights([string]$Endpoint) {
    $pipe = Open-ControlPipe $Endpoint
    $securityDescriptor = [IntPtr]::Zero
    $sidMemory = [IntPtr]::Zero
    try {
        $owner = [IntPtr]::Zero
        $group = [IntPtr]::Zero
        $dacl = [IntPtr]::Zero
        $sacl = [IntPtr]::Zero
        $status = [PtPuvrLifecycleNative]::GetSecurityInfo(
            $pipe.DangerousGetHandle(),
            6,
            0x00000004,
            [ref]$owner,
            [ref]$group,
            [ref]$dacl,
            [ref]$sacl,
            [ref]$securityDescriptor)
        Assert-Equal $status 0 'query host pipe DACL'
        Assert-True ($dacl -ne [IntPtr]::Zero) 'host pipe DACL is present'

        $sid = [Security.Principal.SecurityIdentifier]::new('S-1-5-11')
        $sidBytes = New-Object byte[] $sid.BinaryLength
        $sid.GetBinaryForm($sidBytes, 0)
        $sidMemory = [Runtime.InteropServices.Marshal]::AllocHGlobal($sidBytes.Length)
        [Runtime.InteropServices.Marshal]::Copy($sidBytes, 0, $sidMemory, $sidBytes.Length)
        $trustee = [PtPuvrLifecycleNative+TRUSTEE]::new()
        [PtPuvrLifecycleNative]::BuildTrusteeWithSid([ref]$trustee, $sidMemory)
        [uint32]$rights = 0
        $status = [PtPuvrLifecycleNative]::GetEffectiveRightsFromAcl(
            $dacl,
            [ref]$trustee,
            [ref]$rights)
        Assert-Equal $status 0 'compute Authenticated Users host pipe rights'
        Assert-True (($rights -band 0x00000001) -ne 0) 'Authenticated Users can read pipe data'
        Assert-True (($rights -band 0x00000002) -ne 0) 'Authenticated Users can write pipe data'
        Assert-True (($rights -band 0x00000004) -eq 0) 'Authenticated Users lack FILE_CREATE_PIPE_INSTANCE'
        return $rights
    }
    finally {
        if ($sidMemory -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::FreeHGlobal($sidMemory)
        }
        if ($securityDescriptor -ne [IntPtr]::Zero) {
            [void][PtPuvrLifecycleNative]::LocalFree($securityDescriptor)
        }
        $pipe.Dispose()
    }
}

function New-PrototypePassword {
    $bytes = New-Object byte[] 18
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return 'Puvr!' + [Convert]::ToHexString($bytes) + 'a1'
}

function Quote-PowerShellArgument([string]$Value) {
    return '"' + $Value.Replace('"', '\"') + '"'
}

function New-TestUser([string]$Name) {
    if (Get-LocalUser -Name $Name -ErrorAction SilentlyContinue) {
        throw "Refusing to reuse pre-existing local user: $Name"
    }
    $password = New-PrototypePassword
    $securePassword = ConvertTo-SecureString $password -AsPlainText -Force
    $user = New-LocalUser -Name $Name -Password $securePassword -FullName "Control-plane prototype $Name" -PasswordNeverExpires
    $context = [pscustomobject]@{
        name = $Name
        sid = $user.SID.Value
        credential = [PSCredential]::new($Name, $securePassword)
        layout = $null
        client = $null
        profile = $null
    }
    Assert-True (-not @(
            Get-LocalGroupMember -Group 'Administrators' -ErrorAction Stop |
                Where-Object { $_.SID -eq $context.sid }
        )) "standard user is not administrator $Name"
    $createdUsers.Add($context)
    return $context
}

function Get-ProfilePathForSid([string]$Sid) {
    $profilePath = (Get-ItemProperty -LiteralPath (
        "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$Sid"
    ) -ErrorAction Stop).ProfileImagePath
    Assert-True (-not [string]::IsNullOrWhiteSpace($profilePath)) "profile path for $Sid"
    return [Environment]::ExpandEnvironmentVariables($profilePath)
}

function Initialize-UserLayout([object]$User) {
    # Start once under the real standard-user credential to materialize its profile.
    $profileProcess = Start-Process `
        -FilePath cmd.exe `
        -ArgumentList '/c exit 0' `
        -Credential $User.credential `
        -LoadUserProfile `
        -WorkingDirectory 'C:\Windows\System32' `
        -Wait `
        -PassThru
    Assert-Equal $profileProcess.ExitCode 0 "user profile initialization $($User.name)"

    $User.profile = Get-ProfilePathForSid $User.sid
    $localAppData = Join-Path $User.profile 'AppData\Local'
    $User.layout = Join-Path $localAppData 'Microsoft\PowerToys\WorkspacesControlPlanePrototype'
    $inbox = Join-Path $User.layout 'ReleaseInbox'
    New-Item -ItemType Directory -Path $inbox -Force | Out-Null
    Get-ChildItem -LiteralPath $releaseSetsRoot -Directory | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $inbox $_.Name) -Recurse -Force
    }
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
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $User.layout -AclObject $acl
    & icacls.exe $User.layout /inheritance:r /grant:r `
        "*S-1-5-18:(OI)(CI)F" `
        "*S-1-5-32-544:(OI)(CI)F" `
        "*$($User.sid):(OI)(CI)F" `
        /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not grant the exact user layout ACL for $($User.name)."
    }
    & icacls.exe $User.layout /setowner "*$($User.sid)" /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not set exact user ownership for $($User.name) layout."
    }
    $User.client = $clientPath
    Assert-True (Test-Path -LiteralPath $User.client) "MSI-installed client $($User.name)"
    $owner = (Get-Acl -LiteralPath $User.layout).Owner
    $expectedOwner = ([Security.Principal.SecurityIdentifier]$User.sid).Translate(
        [Security.Principal.NTAccount]).Value
    Assert-Equal $owner $expectedOwner "user layout ownership $($User.name)"
    $signature = Get-AuthenticodeSignature -LiteralPath $User.client
    Assert-Equal $signature.Status 'Valid' "MSI-installed user client signature $($User.name)"
    Assert-Equal (Get-CertificateSha256 $signature.SignerCertificate) $metadata.codeSigner.signerSha256 "MSI-installed user client signer $($User.name)"
}

function Convert-ExitCodeToUInt32([int]$ExitCode) {
    return [BitConverter]::ToUInt32([BitConverter]::GetBytes($ExitCode), 0)
}

function Complete-UserClientProcess(
    [object]$User,
    [string[]]$Arguments,
    [Diagnostics.Process]$Process,
    [string]$StdoutPath,
    [string]$StderrPath,
    [uint32]$ExpectedWin32,
    [AllowNull()][object]$ExpectedDetail
) {
    $stdoutText = if (Test-Path -LiteralPath $StdoutPath) {
        Get-Content -LiteralPath $StdoutPath -Raw
    }
    else {
        ''
    }
    $stderrText = if (Test-Path -LiteralPath $StderrPath) {
        Get-Content -LiteralPath $StderrPath -Raw
    }
    else {
        ''
    }
    $values = [ordered]@{}
    foreach ($line in ($stdoutText -split '\r?\n')) {
        $separator = $line.IndexOf('=')
        if ($separator -gt 0) {
            $values[$line.Substring(0, $separator)] = $line.Substring($separator + 1)
        }
    }
    if (-not $values.Contains('win32') -and
        $stderrText -match 'win32 error=(\d+) operation=([^\r\n]+)') {
        $values.win32 = $Matches[1]
        $values.detail = $Matches[2].Trim()
        $values.transportFailure = 'true'
    }
    if (-not $values.Contains('win32')) {
        throw "user request $($User.name) $($Arguments -join ' ') emitted no parseable status; stdout='$stdoutText'; stderr='$stderrText'"
    }
    Assert-Equal ([uint64]$values.win32) ([uint64]$ExpectedWin32) `
        "user request Win32 status $($User.name) $($Arguments -join ' ')"
    Assert-Equal (Convert-ExitCodeToUInt32 $Process.ExitCode) $ExpectedWin32 `
        "user request process exit $($User.name) $($Arguments -join ' ')"
    if ($null -ne $ExpectedDetail) {
        Assert-Equal ([string]$values.detail) $ExpectedDetail `
            "user request detail $($User.name) $($Arguments -join ' ')"
    }
    return [pscustomobject]@{
        process = $Process
        values = [pscustomobject]$values
        stdout = $stdoutText
        stderr = $stderrText
    }
}

function Invoke-UserClient(
    [object]$User,
    [string[]]$Arguments,
    [uint32]$ExpectedWin32 = 0,
    [AllowNull()][object]$ExpectedDetail = $null
) {
    $outputPrefix = Join-Path $root ("artifacts\lifecycle-client-$($User.name)")
    $stdout = "$outputPrefix.stdout.txt"
    $stderr = "$outputPrefix.stderr.txt"
    Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
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
    return Complete-UserClientProcess `
        $User $Arguments $process $stdout $stderr $ExpectedWin32 $ExpectedDetail
}

function Invoke-UserClientObserved(
    [object]$User,
    [string[]]$Arguments,
    [string]$Suffix
) {
    $outputPrefix = Join-Path $root ("artifacts\lifecycle-observed-$Suffix")
    $stdout = "$outputPrefix.stdout.txt"
    $stderr = "$outputPrefix.stderr.txt"
    Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
    $watch = [Diagnostics.Stopwatch]::StartNew()
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
    $watch.Stop()
    return [pscustomobject]@{
        win32 = Convert-ExitCodeToUInt32 $process.ExitCode
        elapsedMilliseconds = [uint64]$watch.ElapsedMilliseconds
        stdout = if (Test-Path -LiteralPath $stdout) {
            Get-Content -LiteralPath $stdout -Raw
        } else { '' }
        stderr = if (Test-Path -LiteralPath $stderr) {
            Get-Content -LiteralPath $stderr -Raw
        } else { '' }
    }
}

function Start-HoldingUserClient(
    [object]$User,
    [uint32]$Milliseconds,
    [string]$Suffix,
    [switch]$BeforePreface
) {
    $outputPrefix = Join-Path $root ("artifacts\lifecycle-holder-$Suffix")
    $stdout = "$outputPrefix.stdout.txt"
    $stderr = "$outputPrefix.stderr.txt"
    Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
    $process = Start-Process `
        -FilePath $User.client `
        -ArgumentList @(
            if ($BeforePreface) { '--test-hold-before-preface' }
            else { '--test-hold-before-request' }
            [string]$Milliseconds
            '--status'
        ) `
        -Credential $User.credential `
        -LoadUserProfile `
        -WorkingDirectory $User.layout `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -PassThru
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline) {
        if ($process.HasExited) {
            throw "Holding user client exited before connecting: $Suffix"
        }
        if ((Get-Content -LiteralPath $stdout -Raw -ErrorAction SilentlyContinue) -like
            '*testPipeInspectionReady=true*') {
            return [pscustomobject]@{
                process = $process
                stdout = $stdout
                stderr = $stderr
                inspection = Read-Evidence $stdout
            }
        }
        Start-Sleep -Milliseconds 50
    }
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    throw "Holding user client did not connect: $Suffix"
}

function Test-CallerRejections([object]$User, [object]$OtherUser) {
    $hostBefore = Assert-Host '5.0.0.0'
    $nonProxyImage = (Get-Process -Id $PID).Path
    Assert-True (
        $nonProxyImage -match '^[A-Za-z]:\\' -and
        -not $nonProxyImage.StartsWith('\\')
    ) 'deliberately non-proxy client uses a local drive path without UNC access'
    $raw = Open-ControlPipe
    try {
        $nonProxyMilliseconds = Assert-RawPipeRejected $raw 'local-non-proxy-image'
    }
    finally {
        $raw.Dispose()
    }

    $copied = Join-Path $User.layout 'Outside\PtPuvrUserClient.exe'
    New-Item -ItemType Directory -Path (Split-Path $copied -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $User.client -Destination $copied -Force
    $outside = [pscustomobject]@{
        client = $copied
        credential = $User.credential
        name = "$($User.name)-outside"
        layout = $User.layout
    }
    $watch = Invoke-UserClientObserved $outside @('--status') 'outside-protected-path'
    Assert-True ($watch.win32 -in @(109, 233, 995)) `
        'signed proxy outside the protected path is closed before request dispatch'
    Assert-True ($watch.elapsedMilliseconds -lt 5000) `
        'signed proxy path rejection is below the request I/O timeout'

    $sameSidRecovery = Invoke-UserClientObserved $User @('--status') `
        'same-sid-after-path-rejection'
    Assert-Equal $sameSidRecovery.win32 0 `
        'same SID succeeds after its outside-path client is rejected'
    Assert-True ($sameSidRecovery.elapsedMilliseconds -lt 5000) `
        'same SID quickly regains its connection quota after path rejection'

    $otherSid = Invoke-UserClientObserved $OtherUser @('--status') `
        'different-sid-after-non-proxy'
    Assert-Equal $otherSid.win32 1168 `
        'another SID reaches normal request dispatch after non-proxy rejection'
    Assert-True ($otherSid.elapsedMilliseconds -lt 5000) `
        'another SID retains listener capacity after non-proxy rejection'

    $hostAfter = Assert-Host '5.0.0.0'
    Assert-Equal $hostAfter.processId $hostBefore.processId `
        'host PID is stable across pre-read path rejections'
    Assert-Equal $hostAfter.pipeListenerCount $hostBefore.pipeListenerCount `
        'listener count is stable across pre-read path rejections'
    return [ordered]@{
        deliberatelyNonProxyImage = $nonProxyImage
        deliberatelyNonProxyImageKind = 'local-drive'
        deliberatelyNonProxyRejection = 'pre-read transport rejection'
        deliberatelyNonProxyElapsedMilliseconds = $nonProxyMilliseconds
        networkPathAccessRequired = $false
        signedOutsideProtectedPath = 'pre-read transport rejection'
        policy = 'ERROR_ACCESS_DENIED'
        observedWin32 = $watch.win32
        elapsedMilliseconds = $watch.elapsedMilliseconds
        operationWouldOtherwiseSucceed = 'status with an existing SID lease'
        sameSidRecoveryWin32 = [uint32]$sameSidRecovery.win32
        differentSidDispatchWin32 = [uint32]$otherSid.win32
        stableHostProcessId = [uint32]$hostAfter.processId
        stableListenerCount = [uint32]$hostAfter.pipeListenerCount
    }
}

function Assert-ProtectedInstalledClient([object]$User) {
    Assert-Equal ([IO.Path]::GetFullPath($User.client)) ([IO.Path]::GetFullPath($clientPath)) `
        'all users invoke the MSI-installed client'
    $acl = Get-Acl -LiteralPath $clientPath
    Assert-True $acl.AreAccessRulesProtected 'MSI-installed client has a protected DACL'
    Assert-Equal $acl.Owner 'NT AUTHORITY\SYSTEM' 'MSI-installed client owner'
    $usersSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-32-545')
    $usersRules = @(
        $acl.Access | Where-Object {
            $_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]) -eq $usersSid -and
            $_.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow
        }
    )
    Assert-True ($usersRules.Count -ge 1) 'Users have an explicit client allow rule'
    $rights = [Security.AccessControl.FileSystemRights]0
    foreach ($rule in $usersRules) {
        $rights = $rights -bor $rule.FileSystemRights
    }
    Assert-True (
        ($rights -band [Security.AccessControl.FileSystemRights]::ReadAndExecute) -eq
        [Security.AccessControl.FileSystemRights]::ReadAndExecute
    ) 'Users can read and execute the protected client'
    Assert-True (
        ($rights -band (
            [Security.AccessControl.FileSystemRights]::WriteData -bor
            [Security.AccessControl.FileSystemRights]::AppendData -bor
            [Security.AccessControl.FileSystemRights]::WriteAttributes -bor
            [Security.AccessControl.FileSystemRights]::WriteExtendedAttributes -bor
            [Security.AccessControl.FileSystemRights]::Delete
        )) -eq 0
    ) 'Users have no client mutation rights'

    $before = (Get-FileHash -LiteralPath $clientPath -Algorithm SHA256).Hash
    $escapedPath = $clientPath.Replace("'", "''")
    $command = "try { `$stream=[IO.File]::OpenWrite('$escapedPath'); `$stream.Dispose(); exit 0 } catch { exit 5 }"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $probe = Start-Process `
        -FilePath pwsh.exe `
        -ArgumentList @('-NoProfile', '-EncodedCommand', $encoded) `
        -Credential $User.credential `
        -LoadUserProfile `
        -WorkingDirectory $User.layout `
        -Wait `
        -PassThru
    Assert-Equal $probe.ExitCode 5 'standard user cannot open MSI-installed client for write'
    Assert-Equal (Get-FileHash -LiteralPath $clientPath -Algorithm SHA256).Hash $before `
        'MSI-installed client hash unchanged after write probe'
    return [ordered]@{
        path = $clientPath
        owner = $acl.Owner
        sha256 = $before
        standardUserWrite = 'ERROR_ACCESS_DENIED'
    }
}

function Assert-PipeTimeoutAndRestart([object]$User, [object]$OtherUser) {
    $initialEndpoint = Get-PublishedHostEndpoint
    $registryAcl = Get-Acl -LiteralPath $endpointRegistryPath
    Assert-True $registryAcl.AreAccessRulesProtected 'endpoint registry DACL is protected'
    $authenticatedUsersSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-11')
    $registryRules = @(
        $registryAcl.Access | Where-Object {
            $_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]) -eq
                $authenticatedUsersSid -and
            $_.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow
        }
    )
    Assert-True ($registryRules.Count -ge 1) 'Authenticated Users have an endpoint registry allow rule'
    [Security.AccessControl.RegistryRights]$registryRights = 0
    foreach ($rule in $registryRules) {
        $registryRights = $registryRights -bor $rule.RegistryRights
    }
    Assert-True (
        ($registryRights -band [Security.AccessControl.RegistryRights]::ReadKey) -eq
        [Security.AccessControl.RegistryRights]::ReadKey
    ) 'Authenticated Users can read the endpoint registry pointer'
    Assert-True (
        ($registryRights -band (
            [Security.AccessControl.RegistryRights]::SetValue -bor
            [Security.AccessControl.RegistryRights]::CreateSubKey -bor
            [Security.AccessControl.RegistryRights]::Delete -bor
            [Security.AccessControl.RegistryRights]::ChangePermissions -bor
            [Security.AccessControl.RegistryRights]::TakeOwnership
        )) -eq 0
    ) 'Authenticated Users cannot mutate the endpoint registry pointer'

    $hostBefore = Assert-Host '5.0.0.0'
    $hostPidBeforeTimeout = [uint32]$hostBefore.processId
    $inspectionHolder = Start-HoldingUserClient $User 30000 'initial-inspection'
    try {
        [uint32]$listenerCount = $inspectionHolder.inspection.testPipeMaximumInstances
        [uint32]$authenticatedUsersRights = (
            $inspectionHolder.inspection.testPipeAuthenticatedUsersRights
        )
        Assert-True ($listenerCount -gt 1) `
            'authenticated client observes a multi-instance listener bound'
        Assert-True (($authenticatedUsersRights -band 0x00000001) -ne 0) `
            'Authenticated Users can read pipe data'
        Assert-True (($authenticatedUsersRights -band 0x00000002) -ne 0) `
            'Authenticated Users can write pipe data'
        Assert-True (($authenticatedUsersRights -band 0x00000004) -eq 0) `
            'Authenticated Users lack FILE_CREATE_PIPE_INSTANCE'
    }
    finally {
        if (-not $inspectionHolder.process.HasExited) {
            Stop-Process -Id $inspectionHolder.process.Id -Force
        }
        $inspectionHolder.process.WaitForExit()
    }

    $instanceProbe = Open-ControlPipe $initialEndpoint
    try {
        [uint64]$firstRawRejection = Assert-RawPipeRejected `
            $instanceProbe 'first'
    }
    finally {
        $instanceProbe.Dispose()
    }

    $rawRejectionTimes = [System.Collections.Generic.List[uint64]]::new()
    $rawWatch = [Diagnostics.Stopwatch]::StartNew()
    for ($index = 0; $index -lt $listenerCount; $index++) {
        $raw = Open-ControlPipe $initialEndpoint
        try {
            $rawRejectionTimes.Add((Assert-RawPipeRejected $raw "raw-$index"))
        }
        finally {
            $raw.Dispose()
        }
    }
    $rawWatch.Stop()
    Assert-True ($rawWatch.ElapsedMilliseconds -lt 5000) `
        'multiple direct raw clients are rejected without consuming the I/O deadline'

    $quotaHolder = Start-HoldingUserClient $User 30000 'quota-before-preface' -BeforePreface
    $quotaWatch = [Diagnostics.Stopwatch]::StartNew()
    try {
        Start-Sleep -Milliseconds 300
        $sameSid = Invoke-UserClientObserved $User @('--status') 'same-sid-quota'
        Assert-True ($sameSid.win32 -in @(109, 233, 995)) `
            'excess same-SID connection is closed before request dispatch'
        Assert-True ($sameSid.elapsedMilliseconds -lt 5000) `
            'same-SID quota rejection is below the I/O timeout'

        $differentSid = Invoke-UserClientObserved $OtherUser @('--status') 'different-sid-capacity'
        Assert-Equal $differentSid.win32 1168 `
            'different legitimate SID reaches normal owner lookup while one SID is stalled'
        Assert-True ($differentSid.elapsedMilliseconds -lt 5000) `
            'different legitimate SID retains listener capacity'
        Assert-True ($quotaWatch.ElapsedMilliseconds -lt 4500) `
            'same-SID rejection and different-SID success complete while the holder is active'

        $firstSameSidSuccess = $null
        while ($quotaWatch.ElapsedMilliseconds -lt 10000) {
            $observation = Invoke-UserClientObserved `
                $User @('--status') "quota-timeout-$($quotaWatch.ElapsedMilliseconds)"
            if ($observation.win32 -eq 0) {
                $firstSameSidSuccess = [uint64]$quotaWatch.ElapsedMilliseconds
                break
            }
            Assert-True ($observation.win32 -in @(109, 233, 995)) `
                'same-SID connection remains quota-rejected before stalled read expires'
            Start-Sleep -Milliseconds 100
        }
        Assert-True ($null -ne $firstSameSidSuccess) `
            'same SID regains capacity after the stop-aware read deadline'
        Assert-True (
            $firstSameSidSuccess -ge 3500 -and $firstSameSidSuccess -le 10000
        ) 'externally measured stalled read bound remains around five seconds'
    }
    finally {
        if (-not $quotaHolder.process.HasExited) {
            Stop-Process -Id $quotaHolder.process.Id -Force
        }
        $quotaHolder.process.WaitForExit()
    }

    $postInspection = Start-HoldingUserClient $User 30000 'post-inspection'
    try {
        [uint32]$listenerCountAfter = (
            $postInspection.inspection.testPipeMaximumInstances
        )
    }
    finally {
        if (-not $postInspection.process.HasExited) {
            Stop-Process -Id $postInspection.process.Id -Force
        }
        $postInspection.process.WaitForExit()
    }
    $postProbe = Open-ControlPipe $initialEndpoint
    try {
        [void](Assert-RawPipeRejected $postProbe 'post-quota')
    }
    finally {
        $postProbe.Dispose()
    }
    Assert-Equal $listenerCountAfter $listenerCount `
        'externally measured listener bound unchanged after starvation probes'
    $hostAfterQuota = Assert-Host '5.0.0.0'
    Assert-Equal $hostAfterQuota.processId $hostPidBeforeTimeout `
        'host PID unchanged after direct rejection and SID-quota probes'
    Assert-Equal $hostAfterQuota.pipeListenerCount $listenerCount `
        'host evidence listener count matches the external named-pipe bound'
    Assert-Equal $hostAfterQuota.pipePerSidActiveConnectionLimit 1 `
        'observed one active holder matches the published per-SID quota'

    $abandonedAtStartup = Join-Path (
        Join-Path $installRoot 'Staging'
    ) 'release-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
    New-Item -ItemType Directory -Path $abandonedAtStartup -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $abandonedAtStartup 'partial.bin') -Value 'partial'

    $stopHolder = Start-HoldingUserClient $User 30000 'stop'
    $stopWatch = [Diagnostics.Stopwatch]::StartNew()
    try {
        Stop-Service -Name 'PtPuvrHost'
        (Get-Service -Name 'PtPuvrHost').WaitForStatus(
            'Stopped',
            [TimeSpan]::FromSeconds(15))
        $stopWatch.Stop()
        Assert-True ($stopWatch.ElapsedMilliseconds -lt 10000) `
            'host stop joins all listener workers below the read timeout envelope'
    }
    finally {
        if (-not $stopHolder.process.HasExited) {
            Stop-Process -Id $stopHolder.process.Id -Force
        }
        $stopHolder.process.WaitForExit()
    }
    Assert-True (
        -not (Get-ItemProperty -LiteralPath $endpointRegistryPath).PSObject.Properties['HostEndpoint']
    ) 'host stop clears the published endpoint'
    $oldEndpointSquatter = [PtPuvrLifecycleNative]::CreateNamedPipe(
        $initialEndpoint,
        0x00080003,
        0x00000006,
        1,
        4096,
        4096,
        0,
        [IntPtr]::Zero)
    Assert-True (-not $oldEndpointSquatter.IsInvalid) 'old random endpoint can be precreated after stop'
    try {
        Start-Service -Name 'PtPuvrHost'
        (Get-Service -Name 'PtPuvrHost').WaitForStatus(
            'Running',
            [TimeSpan]::FromSeconds(30))
        $newEndpoint = Get-PublishedHostEndpoint
        Assert-True ($newEndpoint -ne $initialEndpoint) 'restart publishes a fresh random endpoint'
        Assert-True (-not (Test-Path -LiteralPath $abandonedAtStartup)) `
            'host startup removes an abandoned release stage'
        Assert-True (
            [uint32](Get-ServiceRecord 'PtPuvrHost').ProcessId -ne $hostPidBeforeTimeout
        ) 'host restart uses a new process'
        Assert-FirstPipeInstanceAnchor $newEndpoint
        $abandonedAtRequest = Join-Path (
            Join-Path $installRoot 'Staging'
        ) 'release-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
        New-Item -ItemType Directory -Path $abandonedAtRequest -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $abandonedAtRequest 'partial.bin') -Value 'partial'
        Invoke-UserClient $User @('--status') | Out-Null
        Assert-True (-not (Test-Path -LiteralPath $abandonedAtRequest)) `
            'next host request removes an abandoned release stage'
    }
    finally {
        $oldEndpointSquatter.Dispose()
    }
    return [ordered]@{
        initialEndpoint = $initialEndpoint
        restartedEndpoint = $newEndpoint
        authenticatedUsersMask = ('0x{0:X8}' -f $authenticatedUsersRights)
        authenticatedUsersCreatePipeInstance = $false
        listenerCount = $listenerCount
        listenerCountAfter = $listenerCountAfter
        directRawClientCount = $listenerCount + 1
        firstDirectRawRejectionMilliseconds = $firstRawRejection
        maximumDirectRawRejectionMilliseconds = (
            $rawRejectionTimes | Measure-Object -Maximum
        ).Maximum
        directRawBatchMilliseconds = [uint64]$rawWatch.ElapsedMilliseconds
        observedPerSidActiveConnectionLimit = 1
        quotaAcquiredBeforeAuthenticationPreface = $true
        sameSidExcessWin32 = [uint32]$sameSid.win32
        differentSidWin32 = [uint32]$differentSid.win32
        measuredReadTimeoutMilliseconds = $firstSameSidSuccess
        hostPidUnchangedDuringTimeout = $true
        stopWhileStalledMilliseconds = [uint64]$stopWatch.ElapsedMilliseconds
        oldEndpointPrecreated = $true
        randomRestartEndpoint = 'completed'
        abandonedReleaseStageRecovery = 'startup-and-next-request'
        firstInstanceSquat = 'ERROR_PIPE_BUSY'
    }
}

function Get-ProtectedStateSnapshot([switch]$ExcludeLeases) {
    $state = [ordered]@{}
    $names = @(
            'active-engine.txt',
            'engine-version-floor.txt',
            'accepted-release-state.txt',
            'runtime-version-floor-track1.txt',
            'runtime-version-floor-track2.txt',
            'leases.txt',
            'runtime-inventory.txt'
        )
    if ($ExcludeLeases) {
        $names = @($names | Where-Object { $_ -ne 'leases.txt' })
    }
    foreach ($name in $names) {
        $path = Join-Path $storeRoot $name
        Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "protected state exists $name"
        $state[$name] = Get-Content -LiteralPath $path -Raw
    }
    return ($state | ConvertTo-Json -Compress)
}

function Get-AcceptedSecurityEpoch {
    $line = Get-Content -LiteralPath (Join-Path $storeRoot 'accepted-release-state.txt') |
        Where-Object { $_ -like 'epoch=*' }
    Assert-Equal @($line).Count 1 'accepted release state epoch record count'
    return [uint64]$line.Substring('epoch='.Length)
}

function Get-LeaseOwners {
    return @(
        Get-Content -LiteralPath (Join-Path $storeRoot 'leases.txt') |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
}

function Invoke-HostUninstallCheck([uint32]$ExpectedWin32) {
    $process = Start-Process `
        -FilePath $hostPath `
        -ArgumentList '--msi-uninstall-check' `
        -Wait `
        -PassThru
    Assert-Equal (Convert-ExitCodeToUInt32 $process.ExitCode) $ExpectedWin32 `
        'direct elevated uninstall precheck Win32 status'
}

function Assert-StandaloneTeardownRefusal(
    [object]$User,
    [object[]]$Certificates
) {
    $stdout = Join-Path $root 'artifacts\standalone-teardown-blocked.stdout.txt'
    $stderr = Join-Path $root 'artifacts\standalone-teardown-blocked.stderr.txt'
    Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
    $process = Start-Process `
        -FilePath pwsh.exe `
        -ArgumentList @(
            '-NoProfile',
            '-ExecutionPolicy', 'Bypass',
            '-File', (Join-Path $root 'Teardown.ps1')
        ) `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -Wait `
        -PassThru
    Assert-True ($process.ExitCode -ne 0) 'standalone teardown refuses a live lease'
    $diagnostic = ((Get-Content -LiteralPath $stdout -Raw -ErrorAction SilentlyContinue) +
        (Get-Content -LiteralPath $stderr -Raw -ErrorAction SilentlyContinue))
    Assert-True (
        $diagnostic -like '*MSI teardown is refused while protected leases remain*'
    ) 'standalone teardown reports the exact lease refusal'
    Assert-True (Get-Service -Name PtPuvrHost -ErrorAction SilentlyContinue) `
        'blocked standalone teardown preserves the host service'
    Assert-True (Test-Path -LiteralPath $installRoot -PathType Container) `
        'blocked standalone teardown preserves Program Files'
    Assert-True (Test-Path -LiteralPath $storeRoot -PathType Container) `
        'blocked standalone teardown preserves ProgramData'
    foreach ($certificate in $Certificates) {
        Assert-True (
            @(Get-CertificateEntries 'Cert:\LocalMachine\TrustedPeople' $certificate.thumbprint).Count -ge 1
        ) "blocked standalone teardown preserves $($certificate.role) trust"
    }
    Invoke-UserClient $User @('--release') 0 'lease released' | Out-Null
    Assert-RuntimeRemoved $User
    return [ordered]@{
        exitCode = $process.ExitCode
        leaseRefusal = $true
        certificatesRemainTrusted = $true
        sameOwnerReleaseSucceeded = $true
    }
}

function Assert-StopAwareEngineQualification([object]$User) {
    $marker = Join-Path $storeRoot 'slow-engine-qualification-5.4.0.0.txt'
    Assert-True (-not (Test-Path -LiteralPath $marker)) `
        'slow qualification marker is absent before the deterministic test'
    $stdout = Join-Path $root 'artifacts\slow-engine-stop.stdout.txt'
    $stderr = Join-Path $root 'artifacts\slow-engine-stop.stderr.txt'
    Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
    $request = Start-Process `
        -FilePath $User.client `
        -ArgumentList @('--acquire', '--release-id', 'release-108-engine-stop') `
        -Credential $User.credential `
        -LoadUserProfile `
        -WorkingDirectory $User.layout `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -PassThru
    $deadline = (Get-Date).AddSeconds(45)
    while ((Get-Date) -lt $deadline -and
        -not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        if ($request.HasExited) {
            throw "Slow qualification request exited early: $(
                Get-Content -LiteralPath $stderr -Raw -ErrorAction SilentlyContinue)"
        }
        Start-Sleep -Milliseconds 100
    }
    Assert-True (Test-Path -LiteralPath $marker -PathType Leaf) `
        'deterministic qualification child entered its long-running operation'
    $qualificationEvidence = Read-Evidence $marker
    Assert-Equal $qualificationEvidence.candidateVersion '5.4.0.0' `
        'slow qualification candidate version'
    [uint32]$qualificationPid = $qualificationEvidence.processId
    $qualificationProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$qualificationPid"
    Assert-True $qualificationProcess 'long-running qualification child exists'
    Assert-True (
        $qualificationProcess.ExecutablePath -like
            '*\Engines\5.4.0.0\PtPuvrUpdater.exe'
    ) 'long-running qualification child uses the protected candidate path'
    Assert-Equal ([uint32]$qualificationProcess.ParentProcessId) `
        ([uint32](Get-ServiceRecord 'PtPuvrHost').ProcessId) `
        'qualification child is directly owned by the stable host'
    $activationJournal = Join-Path $storeRoot 'engine-activation-journal.txt'
    $acquisitionJournal = Join-Path $storeRoot 'acquisition-transaction.txt'
    Assert-True (Test-Path -LiteralPath $activationJournal -PathType Leaf) `
        'qualification stop test has a durable activation journal'
    Assert-True (Test-Path -LiteralPath $acquisitionJournal -PathType Leaf) `
        'qualification stop test has a durable outer acquisition journal'

    $stopWatch = [Diagnostics.Stopwatch]::StartNew()
    Stop-Service -Name PtPuvrHost
    (Get-Service -Name PtPuvrHost).WaitForStatus(
        'Stopped',
        [TimeSpan]::FromSeconds(15))
    $stopWatch.Stop()
    Assert-True ($stopWatch.ElapsedMilliseconds -lt 10000) `
        'service stop aborts qualification well below the 120-second child timeout'
    $request.WaitForExit(10000) | Out-Null
    if (-not $request.HasExited) {
        Stop-Process -Id $request.Id -Force
        $request.WaitForExit()
    }
    Assert-True (-not (Get-Process -Id $qualificationPid -ErrorAction SilentlyContinue)) `
        'kill-on-close qualification job leaves no child process'
    Assert-True (Test-Path -LiteralPath $activationJournal -PathType Leaf) `
        'service stop preserves the activation journal for startup recovery'
    Assert-True (Test-Path -LiteralPath $acquisitionJournal -PathType Leaf) `
        'service stop preserves the acquisition journal for retry recovery'
    Assert-Equal (Get-HostActiveEngine) '5.3.0.0' `
        'stopped qualification does not activate the candidate'

    Start-Service -Name PtPuvrHost
    (Get-Service -Name PtPuvrHost).WaitForStatus(
        'Running',
        [TimeSpan]::FromSeconds(30))
    [void](Assert-Host '5.3.0.0')
    Assert-True (-not (Test-Path -LiteralPath $activationJournal)) `
        'startup rolls back the interrupted pre-switch activation'
    Invoke-UserClient $User @(
        '--acquire', '--release-id', 'release-108-engine-stop'
    ) | Out-Null
    [void](Assert-Host '5.4.0.0')
    [void](Assert-Runtime $User '1.3.0.0')
    Assert-Equal (Get-AcceptedSecurityEpoch) 108 `
        'qualification retry commits the exact release epoch'
    Assert-True (-not (Test-Path -LiteralPath $acquisitionJournal)) `
        'qualification retry converges and clears the outer journal'
    return [ordered]@{
        candidate = '5.4.0.0'
        childProcessId = $qualificationPid
        stopMilliseconds = [uint64]$stopWatch.ElapsedMilliseconds
        timeoutMilliseconds = 120000
        childGone = $true
        journalsPreservedOnStop = $true
        restartRecoveredPrevious = '5.3.0.0'
        retryActivated = '5.4.0.0'
        acceptedEpoch = 108
    }
}

function Remove-ExactPrototypeResidue([string]$Path) {
    $parent = Split-Path -Parent $Path
    $leaf = Split-Path -Leaf $Path
    $escapedLeaf = [regex]::Escape($leaf)
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if (Test-Path -LiteralPath $Path) {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path -LiteralPath $parent -PathType Container) {
            Get-ChildItem -LiteralPath $parent -Force -Directory |
                Where-Object { $_.Name -match "^$escapedLeaf\.PtPuvrDelete-[0-9a-f]{32}$" } |
                ForEach-Object {
                    if ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                        Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
                    }
                    else {
                        Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
                    }
                }
        }
        $remaining = Test-Path -LiteralPath $Path
        if (-not $remaining -and (Test-Path -LiteralPath $parent -PathType Container)) {
            $remaining = @(
                Get-ChildItem -LiteralPath $parent -Force -Directory |
                    Where-Object { $_.Name -match "^$escapedLeaf\.PtPuvrDelete-[0-9a-f]{32}$" }
            ).Count -ne 0
        }
        if (-not $remaining) {
            return
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Exact prototype residue could not be removed: $Path"
}

function Remove-ManagedState {
    foreach ($user in @($createdUsers)) {
        if ($user.client -and (Test-Path -LiteralPath $user.client)) {
            try {
                Invoke-UserClient $user @('--release') | Out-Null
            }
            catch {
                # The finally path continues to the MSI-owned zero-lease enforcement check.
            }
        }
    }
    $hostService = Get-Service -Name 'PtPuvrHost' -ErrorAction SilentlyContinue
    if ($hostService -and $hostService.Status -ne 'Running') {
        try {
            Start-Service -Name 'PtPuvrHost'
            $hostService.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
        }
        catch {
            # The MSI result below reports a remaining protected component instead of masking it.
        }
    }
    $installedProduct = @(
        Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue |
            Where-Object {
                $displayName = $_.PSObject.Properties['DisplayName']
                $null -ne $displayName -and $displayName.Value -eq $metadata.msi.productName
            }
    )
    if ($installedProduct.Count -gt 0) {
        Clear-CleanupOutcome
        $uninstall = Invoke-Msi uninstall 'teardown-msi.log'
        Assert-Equal $uninstall.exitCode 0 'MSI teardown after lease release'
    }
    Assert-Equal @(
        Get-Service -Name 'PtPuvrHost','PtPuvrRuntime_*' -ErrorAction SilentlyContinue
    ).Count 0 'services absent before exact residue cleanup'
    Remove-ExactPrototypeResidue $installRoot
    Remove-ExactPrototypeResidue $storeRoot
    Remove-ExactRegistryKey $endpointRegistryPath
    Remove-ExactRegistryKey $cleanupOutcomeRegistryPath
    foreach ($user in @($createdUsers)) {
        $localUser = Get-LocalUser -Name $user.name -ErrorAction SilentlyContinue
        if ($localUser) {
            $profileKey = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$($localUser.SID.Value)"
            Remove-LocalUser -Name $user.name
            if (Test-Path -LiteralPath $profileKey) {
                Remove-Item -LiteralPath $profileKey -Recurse -Force
            }
        }
        if ($user.profile -and (Test-Path -LiteralPath $user.profile)) {
            Remove-Item -LiteralPath $user.profile -Recurse -Force
        }
    }
    $certificates = Get-Certificates
    Restore-Certificates $certificates
}

function Assert-Teardown {
    Assert-Equal @(Get-Service -Name 'PtPuvrHost','PtPuvrRuntime_*' -ErrorAction SilentlyContinue).Count 0 'prototype services removed'
    Assert-True (-not (Test-Path -LiteralPath $installRoot)) 'Program Files root removed'
    Assert-True (-not (Test-Path -LiteralPath $storeRoot)) 'ProgramData root removed'
    Assert-True (-not (Test-Path -LiteralPath $endpointRegistryPath)) 'endpoint registry key removed'
    Assert-True (-not (Test-Path -LiteralPath $cleanupOutcomeRegistryPath)) 'cleanup outcome registry key removed'
    Assert-Equal @(
        Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue |
            Where-Object {
                $displayName = $_.PSObject.Properties['DisplayName']
                $null -ne $displayName -and $displayName.Value -eq $metadata.msi.productName
            }
    ).Count 0 'MSI product removed'
    foreach ($name in $ownerNames) {
        Assert-True (-not (Get-LocalUser -Name $name -ErrorAction SilentlyContinue)) "test user removed $name"
    }
    return [ordered]@{
        services = 0
        installRootPresent = $false
        storeRootPresent = $false
        msiPresent = $false
        localUsers = 0
    }
}

function Invoke-Validation {
    $certificates = Get-Certificates
    $events = [System.Collections.Generic.List[object]]::new()
    $result = [ordered]@{
        timestamp = (Get-Date).ToString('o')
        topology = [ordered]@{
            host = 'PtPuvrHost.exe stable LocalSystem SCM service'
            engine = 'PtPuvrUpdater.exe versioned on-demand LocalSystem child'
            bootstrap = 'WiX v5 per-machine companion MSI'
            userClient = 'MSI-owned Program Files PtPuvrUserClient.exe'
            packageIdentityRequired = $false
        }
        events = $events
        verdict = 'FAIL'
    }
    $failure = $null
    try {
        $trustSubjectNotTrusted = [Convert]::ToUInt32('800B0004', 16)
        $trustNoSignature = [Convert]::ToUInt32('800B0100', 16)
        Ensure-CertificatesTrusted $certificates
        Assert-True (-not (Get-Service -Name 'PtPuvrHost' -ErrorAction SilentlyContinue)) 'no pre-existing host service'
        foreach ($name in $ownerNames) {
            Assert-True (-not (Get-LocalUser -Name $name -ErrorAction SilentlyContinue)) "no pre-existing test user $name"
        }
        $install = Invoke-Msi install 'install-lifecycle.log'
        Assert-Equal $install.exitCode 0 'companion MSI install'
        $msiFiles = @(Get-MsiFileRows)
        foreach ($mutableFile in @(
                'active-engine.txt',
                'engine-version-floor.txt',
                'runtime-version-floor-track1.txt',
                'runtime-version-floor-track2.txt',
                'accepted-release-state.txt',
                'leases.txt',
                'runtime-inventory.txt'
            )) {
            Assert-True ($mutableFile -notin $msiFiles) `
                "mutable state is absent from the MSI File table: $mutableFile"
        }
        $msiProduct = Assert-MsiRegistered
        $hostState = Assert-Host '5.0.0.0'
        $installedSignature = Get-AuthenticodeSignature -LiteralPath $hostPath
        Assert-Equal $installedSignature.Status 'Valid' 'MSI-installed host signature'
        Assert-Equal (Get-CertificateSha256 $installedSignature.SignerCertificate) $metadata.codeSigner.signerSha256 'MSI-installed host signer'
        $msiSignature = Get-AuthenticodeSignature -LiteralPath $msiPath
        Assert-Equal $msiSignature.Status 'Valid' 'companion MSI signature'
        Assert-Equal (Get-CertificateSha256 $msiSignature.SignerCertificate) $metadata.codeSigner.signerSha256 'companion MSI signer'
        Assert-True (Test-Path -LiteralPath $clientPath -PathType Leaf) 'MSI-installed user client payload'
        Assert-True (
            Select-String -LiteralPath $install.log -Pattern 'ServiceInstall' -Quiet
        ) 'MSI declarative ServiceInstall log evidence'
        $failureActions = (& sc.exe qfailure PtPuvrHost 2>&1 | Out-String)
        Assert-Equal $LASTEXITCODE 0 'SCM failure action query'
        Assert-True (
            ([regex]::Matches(
                $failureActions,
                'RESTART\s+-- Delay = 5000 milliseconds')).Count -eq 3
        ) 'SCM has three declarative five-second restart actions'
        $failureActionsFlag = (& sc.exe qfailureflag PtPuvrHost 2>&1 | Out-String)
        Assert-Equal $LASTEXITCODE 0 'SCM non-crash failure action query'
        Assert-True (
            $failureActionsFlag -match 'FAILURE_ACTIONS_ON_NONCRASH_FAILURES\s*:\s*TRUE'
        ) 'SCM applies failure actions to non-crash service failures'
        Assert-Equal (Get-AcceptedSecurityEpoch) 100 'MSI-seeded accepted security epoch'
        Assert-True (
            -not (Test-Path -LiteralPath (Join-Path $storeRoot 'accepted-security-epoch.txt')) -and
            -not (Test-Path -LiteralPath (Join-Path $storeRoot 'accepted-release-metadata.txt'))
        ) 'split accepted-security state is absent'
        Assert-FirstPipeInstanceAnchor
        $mutableReplacementRecovery = Assert-MutableStateReplacementRecovery
        $partialState = Assert-PartialStateFailsClosed
        $events.Add([ordered]@{
            area = 'trusted-bootstrap'
            msi = $msiPath
            msiSha256 = (Get-FileHash -LiteralPath $msiPath -Algorithm SHA256).Hash
            product = $msiProduct.DisplayName
            service = $hostState
            serviceInstallLogged = $true
            userClientPath = $clientPath
            scmFailureActions = 'restart/restart/restart at 5000ms'
            scmFailureActionsOnNonCrash = $true
            initialAcceptedEpoch = 100
            mutableStateMsiFileRows = 0
            mutableReplacementRecovery = $mutableReplacementRecovery
            partialState = $partialState
            controllerBootstrapUsed = $false
        })

        $ownerA = New-TestUser $ownerNames[0]
        $ownerB = New-TestUser $ownerNames[1]
        Initialize-UserLayout $ownerA
        Initialize-UserLayout $ownerB
        $protectedClient = Assert-ProtectedInstalledClient $ownerA
        Assert-Equal $ownerA.client $ownerB.client 'users share the protected MSI proxy'
        Invoke-UserClient $ownerB @('--status') 1168 'caller lease status policy' | Out-Null

        Invoke-UserClient $ownerA @('--acquire', '--release-id', 'release-101') | Out-Null
        $runtimeA100 = Assert-Runtime $ownerA '1.0.0.0' 'not-configured'
        Assert-Equal (Get-AcceptedSecurityEpoch) 101 'release-101 exact accepted epoch'

        $sameReleaseBefore = Get-ProtectedStateSnapshot
        $sameReleaseRetry = Invoke-UserClient $ownerA @('--acquire', '--release-id', 'release-101')
        Assert-Equal $sameReleaseRetry.values.leaseCount 1 'same SID retains one lease on retry'
        Assert-Equal (Get-ProtectedStateSnapshot) $sameReleaseBefore `
            'same-release retry preserves exact durable state'

        $ownerARelease101 = Join-Path $ownerA.layout 'ReleaseInbox\release-101'
        $collisionSource = Join-Path $ownerA.layout 'ReleaseInbox\release-101-collision'
        Remove-Item -LiteralPath $ownerARelease101 -Recurse -Force
        Copy-Item -LiteralPath $collisionSource -Destination $ownerARelease101 -Recurse
        try {
            $collisionBefore = Get-ProtectedStateSnapshot
            Invoke-UserClient $ownerA @('--acquire', '--release-id', 'release-101') `
                1306 'accepted release metadata collision' | Out-Null
            Assert-Equal (Get-ProtectedStateSnapshot) $collisionBefore `
                'release-ID collision does not advance durable state'
        }
        finally {
            Remove-Item -LiteralPath $ownerARelease101 -Recurse -Force
            Copy-Item `
                -LiteralPath (Join-Path $releaseSetsRoot 'release-101') `
                -Destination $ownerARelease101 `
                -Recurse
        }

        $events.Add([ordered]@{
            area = 'caller-authorization'
            userA = [ordered]@{ sid = $ownerA.sid; layout = $ownerA.layout }
            userB = [ordered]@{ sid = $ownerB.sid; layout = $ownerB.layout }
            protectedClient = $protectedClient
            rejections = Test-CallerRejections $ownerA $ownerB
            differentUserWithoutLease = 'ERROR_NOT_FOUND'
            sameUserProxySecrecyRequired = $false
        })

        $events.Add([ordered]@{
            area = 'host-pipe-lifecycle'
            evidence = Assert-PipeTimeoutAndRestart $ownerA $ownerB
        })

        $failedFirstBefore = Get-ProtectedStateSnapshot -ExcludeLeases
        Invoke-UserClient $ownerB @('--acquire', '--release-id', 'release-103-readiness') `
            1062 'runtime service readiness' | Out-Null
        Assert-Equal (Get-ProtectedStateSnapshot -ExcludeLeases) $failedFirstBefore `
            'failed first acquisition preserves epoch floors and inventory'
        Assert-True ((Get-LeaseOwners) -contains $ownerB.sid) `
            'failed first acquisition retains durable SID lease'
        Assert-RuntimeRemoved $ownerB
        Invoke-HostUninstallCheck 170
        $blockedStandaloneTeardown = Assert-StandaloneTeardownRefusal $ownerB $certificates
        Assert-True ((Get-LeaseOwners) -notcontains $ownerB.sid) `
            'release removes failed-first-acquisition lease'

        Invoke-UserClient $ownerB @('--ensure', '--release-id', 'release-101') | Out-Null
        $runtimeA100 = Assert-Runtime $ownerA '1.0.0.0'
        $runtimeB100 = Assert-Runtime $ownerB '1.0.0.0'
        Assert-True ($runtimeA100.serviceName -ne $runtimeB100.serviceName) 'derived service separation'
        Assert-True ($runtimeA100.store -ne $runtimeB100.store) 'derived store separation'
        $leaseOwners = Get-LeaseOwners
        Assert-Equal $leaseOwners.Count 2 'one lease per SID count'
        Assert-Equal @($leaseOwners | Sort-Object -Unique).Count 2 'lease owner uniqueness'
        $events.Add([ordered]@{
            area = 'normal-user-leases'
            leases = $leaseOwners
            maximum = 32
            key = 'caller SID only'
            failedFirstAcquireRelease = 'safe without runtime'
            blockedStandaloneTeardown = $blockedStandaloneTeardown
            userA = $runtimeA100
            userB = $runtimeB100
        })

        $ownerBInventory = @(
            Get-Content -LiteralPath (Join-Path $storeRoot 'runtime-inventory.txt') |
                Where-Object { $_ -like "$($ownerB.sid)|*" }
        )
        Assert-Equal $ownerBInventory.Count 1 `
            'one owner-B inventory row before cleanup .new recovery'
        $ownerBFields = $ownerBInventory[0] -split '\|'
        Assert-Equal $ownerBFields.Count 5 'hash-bound inventory field count'
        $cleanupReplacement = Join-Path $storeRoot 'runtime-cleanup-transaction.txt.new'
        @(
            "owner=$($ownerB.sid)"
            "service=$(Get-RuntimeServiceName $ownerB.sid)"
            "track=$($ownerBFields[1])"
            "version=$($ownerBFields[2])"
            "runtimeSha256=$($ownerBFields[3])"
            "transactionId=$($ownerBFields[4])"
            'phase=prepared'
            ''
        ) -join "`r`n" |
            Set-Content -LiteralPath $cleanupReplacement -Encoding utf8NoBOM -NoNewline
        Invoke-UserClient $ownerB @('--status') 1168 'owner inventory lookup' | Out-Null
        Assert-True (
            -not (Test-Path -LiteralPath (
                Join-Path $storeRoot 'runtime-cleanup-transaction.txt'
            )) -and
            -not (Test-Path -LiteralPath $cleanupReplacement)
        ) 'valid cleanup journal .new is promoted, completed, and cleared'
        Assert-RuntimeRemoved $ownerB
        Assert-True ((Get-LeaseOwners) -contains $ownerB.sid) `
            'cleanup .new recovery preserves the independent owner lease'
        Invoke-UserClient $ownerB @('--acquire', '--release-id', 'release-101') | Out-Null
        [void](Assert-Runtime $ownerB '1.0.0.0')

        $boundedSourceStatuses = [System.Collections.Generic.List[object]]::new()
        $boundedSourceCases = @(
            [pscustomobject]@{
                kind = 'manifest'
                release = 'release-209-size-mismatch'
                file = 'PtPuvrReleaseManifest.exe'
                maximum = [long](1MB)
            }
            [pscustomobject]@{
                kind = 'runtime'
                release = 'release-209-size-mismatch'
                file = 'PtPuvrRuntime-Track1-1.1.0.0.exe'
                maximum = [long](64MB)
            }
            [pscustomobject]@{
                kind = 'engine'
                release = 'release-102'
                file = 'PtPuvrEngine-5.1.0.0.exe'
                maximum = [long](64MB)
            }
        )
        foreach ($case in $boundedSourceCases) {
            $source = Join-Path (
                Join-Path $releaseSetsRoot $case.release
            ) $case.file
            $destination = Join-Path (
                Join-Path $ownerA.layout "ReleaseInbox\$($case.release)"
            ) $case.file
            try {
                foreach ($boundary in @(
                        [pscustomobject]@{
                            name = 'zero'
                            length = [long]0
                            code = [uint32]38
                            detail = 'candidate source nonempty policy'
                        }
                        [pscustomobject]@{
                            name = 'maximum-plus-one'
                            length = [long]$case.maximum + 1
                            code = [uint32]223
                            detail = 'candidate source maximum size policy'
                        }
                    )) {
                    $stream = [IO.File]::Open(
                        $destination,
                        [IO.FileMode]::Create,
                        [IO.FileAccess]::Write,
                        [IO.FileShare]::None)
                    try {
                        $stream.SetLength($boundary.length)
                    }
                    finally {
                        $stream.Dispose()
                    }
                    $before = Get-ProtectedStateSnapshot
                    Invoke-UserClient $ownerA @(
                        '--acquire', '--release-id', $case.release
                    ) $boundary.code $boundary.detail | Out-Null
                    Assert-Equal (Get-ProtectedStateSnapshot) $before `
                        "$($case.kind) $($boundary.name) source preserves exact durable state"
                    [void](Assert-Runtime $ownerA '1.0.0.0')
                    $boundedSourceStatuses.Add([ordered]@{
                        source = $case.kind
                        boundary = $boundary.name
                        win32 = $boundary.code
                        detail = $boundary.detail
                    })
                }
            }
            finally {
                Copy-Item -LiteralPath $source -Destination $destination -Force
            }
        }

        $negativeReleases = @(
            [pscustomobject]@{ id = 'release-201-metadata-signer'; code = $trustSubjectNotTrusted; detail = 'candidate WinVerifyTrust leaf signer pin policy' }
            [pscustomobject]@{ id = 'release-202-tampered-manifest'; code = $trustNoSignature; detail = 'WinVerifyTrust(LocalMachine Authenticode chain)' }
            [pscustomobject]@{ id = 'release-203-hash-mismatch'; code = 23; detail = 'release runtime SHA-256 policy' }
            [pscustomobject]@{ id = 'release-204-traversal'; code = 13; detail = 'release manifest runtime artifact' }
            [pscustomobject]@{ id = 'release-205-stale'; code = 1306; detail = 'accepted security epoch replay policy' }
            [pscustomobject]@{ id = 'release-206-host-floor'; code = 1150; detail = 'release manifest host version floor' }
            [pscustomobject]@{ id = 'release-207-code-signer'; code = $trustSubjectNotTrusted; detail = 'candidate WinVerifyTrust leaf signer pin policy' }
            [pscustomobject]@{ id = 'release-209-size-mismatch'; code = 24; detail = 'candidate source exact size policy' }
        )
        foreach ($negative in $negativeReleases) {
            $before = Get-ProtectedStateSnapshot
            Invoke-UserClient $ownerA @('--acquire', '--release-id', $negative.id) `
                $negative.code $negative.detail | Out-Null
            Assert-Equal (Get-ProtectedStateSnapshot) $before `
                "negative release does not advance protected state $($negative.id)"
            [void](Assert-Runtime $ownerA '1.0.0.0')
        }
        $events.Add([ordered]@{
            area = 'signed-release-metadata-negative'
            boundedSourceStatuses = @($boundedSourceStatuses)
            exactStatuses = @($negativeReleases | ForEach-Object {
                    [ordered]@{ release = $_.id; win32 = [uint32]$_.code; detail = $_.detail }
                })
            durableStateAdvanced = $false
        })

        Invoke-UserClient $ownerA @('--acquire', '--release-id', 'release-102') | Out-Null
        $runtimeA110 = Assert-Runtime $ownerA '1.1.0.0'
        [void](Assert-Runtime $ownerB '1.0.0.0')
        [void](Assert-Host '5.1.0.0')
        Assert-Equal (Get-AcceptedSecurityEpoch) 102 'release-102 exact accepted epoch'

        $repairBefore = Get-ProtectedStateSnapshot
        $repair = Invoke-Msi repair 'forced-repair-lifecycle.log'
        Assert-Equal $repair.exitCode 0 'forced MSI repair'
        (Get-Service -Name 'PtPuvrHost').WaitForStatus(
            'Running',
            [TimeSpan]::FromSeconds(30))
        Assert-Equal (Get-ProtectedStateSnapshot) $repairBefore `
            'forced repair preserves all mutable security and inventory state'
        [void](Assert-Host '5.1.0.0')
        [void](Assert-Runtime $ownerA '1.1.0.0')
        [void](Assert-Runtime $ownerB '1.0.0.0')

        $featureRemovalBefore = Get-ProtectedStateSnapshot
        $featureRemoval = Invoke-Msi remove-feature 'blocked-feature-removal.log'
        Assert-Equal $featureRemoval.exitCode 1603 `
            'feature-level removal cannot bypass the lease guard'
        Assert-Equal (Get-ProtectedStateSnapshot) $featureRemovalBefore `
            'blocked feature removal preserves mutable state'
        [void](Assert-MsiRegistered)
        [void](Assert-Host '5.1.0.0')
        [void](Assert-Runtime $ownerA '1.1.0.0')
        [void](Assert-Runtime $ownerB '1.0.0.0')
        $events.Add([ordered]@{
            area = 'msi-maintenance'
            mutableFileRows = 0
            forcedRepairExitCode = $repair.exitCode
            mutableStatePreserved = $true
            blockedFeatureRemovalExitCode = $featureRemoval.exitCode
            leaseGuardBypass = $false
            servicesCoherent = $true
        })

        $sameVersionCollisionBefore = Get-ProtectedStateSnapshot
        Invoke-UserClient $ownerA @(
            '--acquire', '--release-id', 'release-109-same-version-collision'
        ) 80 'runtime version collision policy' | Out-Null
        Assert-Equal (Get-ProtectedStateSnapshot) $sameVersionCollisionBefore `
            'same-version byte-different collision preserves exact durable state'
        [void](Assert-Runtime $ownerA '1.1.0.0')

        $readinessBefore = Get-ProtectedStateSnapshot
        Invoke-UserClient $ownerA @('--acquire', '--release-id', 'release-103-readiness') `
            1062 'runtime service readiness' | Out-Null
        Assert-Equal (Get-ProtectedStateSnapshot) $readinessBefore `
            'readiness failure rolls back outer metadata and floor state'
        [void](Assert-Runtime $ownerA '1.1.0.0')

        $qualificationBefore = Get-ProtectedStateSnapshot
        Invoke-UserClient $ownerA @('--acquire', '--release-id', 'release-104-engine-fail') `
            1062 'engine qualification readiness' | Out-Null
        Invoke-UserClient $ownerA @('--status') | Out-Null
        Assert-Equal (Get-ProtectedStateSnapshot) $qualificationBefore `
            'engine qualification failure preserves outer metadata and floors'
        [void](Assert-Host '5.1.0.0')
        $events.Add([ordered]@{
            area = 'engine-self-servicing'
            initial = '5.0.0.0'
            healthyUpgrade = '5.1.0.0'
            readinessFailure = '5.2.0.0 rejected'
            stableHostImagePath = (Get-ServiceRecord 'PtPuvrHost').PathName
        })

        Invoke-UserClient $ownerA @('--acquire', '--release-id', 'release-105-engine-before') `
            109 'ReadFile(user client response)' | Out-Null
        $activationJournal = Join-Path $storeRoot 'engine-activation-journal.txt'
        Move-Item -LiteralPath $activationJournal -Destination "$activationJournal.new"
        $outerJournal = Join-Path $storeRoot 'acquisition-transaction.txt'
        Copy-Item -LiteralPath $outerJournal -Destination "$outerJournal.new"
        Restart-HostAfterCrash
        [void](Assert-Host '5.1.0.0')
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $storeRoot 'engine-activation-journal.txt'))) 'pre-switch activation journal recovered'
        Assert-True (-not (Test-Path -LiteralPath "$activationJournal.new")) `
            'valid engine activation journal .new was promoted and recovered'
        Invoke-UserClient $ownerA @('--status') | Out-Null
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $storeRoot 'acquisition-transaction.txt'))) 'pre-switch outer acquisition journal reconciled'
        Assert-True (-not (Test-Path -LiteralPath "$outerJournal.new")) `
            'primary acquisition journal was authoritative over stale .new'
        Invoke-UserClient $ownerA @('--acquire', '--release-id', 'release-106-engine-after') `
            109 'ReadFile(user client response)' | Out-Null
        Copy-Item -LiteralPath $activationJournal -Destination "$activationJournal.new"
        Restart-HostAfterCrash
        [void](Assert-Host '5.3.0.0')
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $storeRoot 'engine-activation-journal.txt'))) 'post-switch activation journal recovered'
        Assert-True (-not (Test-Path -LiteralPath "$activationJournal.new")) `
            'primary engine activation journal was authoritative over stale .new'
        Invoke-UserClient $ownerA @('--status') | Out-Null
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $storeRoot 'acquisition-transaction.txt'))) 'post-switch outer acquisition journal reconciled'
        Invoke-UserClient $ownerA @('--acquire', '--release-id', 'release-106-engine-after') | Out-Null
        [void](Assert-Runtime $ownerA '1.1.0.0')
        Assert-Equal (Get-AcceptedSecurityEpoch) 106 'engine-crash release commits exact epoch on retry'
        $events.Add([ordered]@{
            area = 'engine-activation-crash-recovery'
            beforeActiveSwitch = 'rolled back to 5.1.0.0'
            afterActiveSwitch = 'recovered at 5.3.0.0'
            journal = 'cleared'
        })

        Invoke-UserClient $ownerA @('--acquire', '--release-id', 'release-107-runtime-crash') `
            1067 'engine exited without a protected response' | Out-Null
        $runtimeJournal = Join-Path $storeRoot 'runtime-transaction.txt'
        $acquisitionJournal = Join-Path $storeRoot 'acquisition-transaction.txt'
        Assert-True (Test-Path -LiteralPath $runtimeJournal -PathType Leaf) `
            'target runtime crash left the inner transaction journal'
        Assert-True (
            Select-String -LiteralPath $runtimeJournal -SimpleMatch 'phase=inventory-committed' -Quiet
        ) 'target runtime crash reached inventory-committed phase'
        Assert-True (Test-Path -LiteralPath $acquisitionJournal -PathType Leaf) `
            'target runtime crash left the outer acquisition journal'
        Assert-True (
            Select-String -LiteralPath $acquisitionJournal -SimpleMatch 'phase=runtime-provisioning' -Quiet
        ) 'outer acquisition journal records runtime-provisioning phase'
        Assert-True ((Get-LeaseOwners) -contains $ownerA.sid) `
            'durable SID lease exists after runtime process termination'
        Assert-True (
            Select-String `
                -LiteralPath (Join-Path $storeRoot 'runtime-inventory.txt') `
                -SimpleMatch "$($ownerA.sid)|1|1.3.0.0" `
                -Quiet
        ) 'independent protected inventory records crash target'
        $crashService = Get-ServiceRecord (Get-RuntimeServiceName $ownerA.sid)
        Assert-True ($crashService.PathName -like '*\Runtimes\Track1\1.3.0.0\PtPuvrRuntime.exe*') `
            'independent SCM config records crash target'

        Move-Item -LiteralPath $runtimeJournal -Destination "$runtimeJournal.new"
        Move-Item -LiteralPath $acquisitionJournal -Destination "$acquisitionJournal.new"
        Invoke-HostUninstallCheck 170
        $blockedUninstall = Invoke-Msi uninstall 'blocked-uninstall-runtime-crash.log'
        Assert-Equal $blockedUninstall.exitCode 1603 `
            'MSI uninstall exact failure while lease/journals/runtime remain'
        Assert-True (Test-Path -LiteralPath $installRoot -PathType Container) `
            'blocked uninstall preserves Program Files root'
        Assert-True (Test-Path -LiteralPath $storeRoot -PathType Container) `
            'blocked uninstall preserves ProgramData root'
        Assert-True (Get-Service -Name 'PtPuvrHost' -ErrorAction SilentlyContinue) `
            'blocked uninstall preserves host registration'
        if ((Get-Service -Name 'PtPuvrHost').Status -ne 'Running') {
            Start-Service -Name 'PtPuvrHost'
            (Get-Service -Name 'PtPuvrHost').WaitForStatus(
                'Running',
                [TimeSpan]::FromSeconds(30))
        }

        Invoke-UserClient $ownerA @('--acquire', '--release-id', 'release-107-runtime-crash') | Out-Null
        [void](Assert-Runtime $ownerA '1.3.0.0')
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $storeRoot 'runtime-transaction.txt'))) 'runtime transaction journal recovered'
        Assert-True (-not (Test-Path -LiteralPath $acquisitionJournal)) 'outer acquisition journal recovered'
        Assert-True (
            -not (Test-Path -LiteralPath "$runtimeJournal.new") -and
            -not (Test-Path -LiteralPath "$acquisitionJournal.new")
        ) 'valid runtime and acquisition journal .new remnants promoted and converged'
        Assert-Equal (Get-AcceptedSecurityEpoch) 107 'runtime crash recovery commits exact epoch'
        Assert-Equal (
            Get-Content -LiteralPath (Join-Path $storeRoot 'runtime-version-floor-track1.txt') -Raw
        ) '1.3.0.0' 'runtime crash recovery commits target floor'

        $downgradeBefore = Get-ProtectedStateSnapshot
        Invoke-UserClient $ownerB @('--acquire', '--release-id', 'release-208-runtime-downgrade') `
            1306 'release runtime version floor' | Out-Null
        Assert-Equal (Get-ProtectedStateSnapshot) $downgradeBefore `
            'runtime downgrade does not advance protected state'
        [void](Assert-Runtime $ownerB '1.0.0.0')
        $events.Add([ordered]@{
            area = 'runtime-transactions-and-floors'
            runtimeCrashPhase = 'after-inventory-before-sync'
            runtimeCrashWin32 = 1067
            durableLeaseAfterCrash = $true
            innerJournalObserved = 'inventory-committed'
            outerJournalObserved = 'runtime-provisioning'
            blockedUninstallExitCode = 1603
            runtimeCrashRecovery = 'reconciled to target'
            runtimeFloor = (Get-Content -LiteralPath (Join-Path $storeRoot 'runtime-version-floor-track1.txt') -Raw)
            acceptedEpoch = Get-AcceptedSecurityEpoch
            downgradeWin32 = 1306
        })

        $events.Add([ordered]@{
            area = 'stop-aware-engine-qualification'
            evidence = Assert-StopAwareEngineQualification $ownerA
        })

        $activeEnginePath = Join-Path $installRoot 'Engines\5.4.0.0\PtPuvrUpdater.exe'
        $exactEngineRetryBefore = Get-ProtectedStateSnapshot
        $activeEngineHashBefore = (Get-FileHash -LiteralPath $activeEnginePath -Algorithm SHA256).Hash
        $exactEngineRetry = Invoke-UserClient $ownerA @(
            '--acquire', '--release-id', 'release-108-engine-stop'
        )
        Assert-Equal $exactEngineRetry.values.leaseCount 1 `
            'exact same-version engine retry retains one SID lease'
        Assert-Equal (Get-ProtectedStateSnapshot) $exactEngineRetryBefore `
            'exact same-version engine retry preserves exact durable state'
        Assert-Equal (
            Get-FileHash -LiteralPath $activeEnginePath -Algorithm SHA256
        ).Hash $activeEngineHashBefore 'exact same-version engine retry preserves active bytes'
        [void](Assert-Host '5.4.0.0')
        [void](Assert-Runtime $ownerA '1.3.0.0')

        $engineCollisionBefore = Get-ProtectedStateSnapshot
        $engineCollisionRuntimeBefore = Assert-Runtime $ownerA '1.3.0.0'
        $engineCollisionServiceBefore = Get-ServiceRecord $engineCollisionRuntimeBefore.serviceName
        $engineCollision = Invoke-UserClient $ownerA @(
            '--acquire', '--release-id', 'release-110-engine-version-collision'
        ) 80 'engine version collision policy'
        Assert-Equal (Get-ProtectedStateSnapshot) $engineCollisionBefore `
            'equal-version byte-different engine preserves leases, floors, accepted state, active state, and inventory'
        Assert-Equal (
            Get-FileHash -LiteralPath $activeEnginePath -Algorithm SHA256
        ).Hash $activeEngineHashBefore 'equal-version byte-different engine preserves active bytes'
        $engineCollisionRuntimeAfter = Assert-Runtime $ownerA '1.3.0.0'
        $engineCollisionServiceAfter = Get-ServiceRecord $engineCollisionRuntimeAfter.serviceName
        Assert-Equal $engineCollisionServiceAfter.ProcessId $engineCollisionServiceBefore.ProcessId `
            'equal-version byte-different engine preserves runtime process'
        Assert-Equal $engineCollisionServiceAfter.PathName $engineCollisionServiceBefore.PathName `
            'equal-version byte-different engine preserves runtime image path'
        [void](Assert-Host '5.4.0.0')
        $events.Add([ordered]@{
            area = 'engine-same-version-policy'
            exactRetry = [ordered]@{
                release = 'release-108-engine-stop'
                version = '5.4.0.0'
                byteEquality = 'accepted'
                durableStatePreserved = $true
            }
            collision = [ordered]@{
                release = 'release-110-engine-version-collision'
                version = '5.4.0.0'
                win32 = [uint32]$engineCollision.values.win32
                detail = $engineCollision.values.detail
                checkedBeforeDurableAdvancement = $true
                leasesFloorsAcceptedStateInventoryPreserved = $true
                runtimeProcessId = [uint32]$engineCollisionRuntimeAfter.processId
                activeEngineSha256 = $activeEngineHashBefore
            }
        })

        Invoke-UserClient $ownerA @('--release') 0 'lease released' | Out-Null
        Assert-RuntimeRemoved $ownerA
        [void](Assert-Runtime $ownerB '1.0.0.0' 'not-configured')
        Invoke-UserClient $ownerB @('--status') | Out-Null
        Invoke-UserClient $ownerB @('--release') 0 'lease released' | Out-Null
        Assert-RuntimeRemoved $ownerB
        Assert-Equal (Get-LeaseOwners).Count 0 'zero protected leases'
        Assert-Equal @(
            Get-Content -LiteralPath (Join-Path $storeRoot 'runtime-inventory.txt') |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        ).Count 0 'zero runtime inventory entries'
        Assert-Equal @(
            Get-Service -Name 'PtPuvrRuntime_*' -ErrorAction SilentlyContinue
        ).Count 0 'zero dynamic runtime services before uninstall'
        foreach ($journal in 'runtime-transaction.txt', 'runtime-cleanup-transaction.txt',
            'acquisition-transaction.txt', 'engine-activation-journal.txt') {
            Assert-True (-not (Test-Path -LiteralPath (Join-Path $storeRoot $journal))) `
                "no pending journal before uninstall: $journal"
        }
        Invoke-HostUninstallCheck 0
        $events.Add([ordered]@{
            area = 'last-uninstall'
            machineUninstallWithLeaseAndJournals = 'ERROR_INSTALL_FAILURE (1603)'
            userARelease = 'user B runtime intact'
            userBLastRelease = 'final runtime removed'
            protectedLeaseState = 'zero'
            runtimeInventory = 'zero'
            runtimeServices = 'zero'
            elevatedPreRemoveCheck = 'ERROR_SUCCESS'
        })
        $events.Add([ordered]@{
            area = 'msi-commit-cleanup'
            evidence = Invoke-RawMsiUninstallAndAssertCommitCleanup
        })
    }
    catch {
        $failure = $_
    }
    try {
        Remove-ManagedState
        $result.teardown = Assert-Teardown
    }
    catch {
        if (-not $failure) {
            $failure = $_
        }
        $result.teardown = [ordered]@{ error = $_.Exception.Message }
    }
    if ($failure) {
        $result.failure = $failure.Exception.Message
    }
    else {
        $result.verdict = 'PASS'
    }
    $result | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $resultPath -Encoding utf8NoBOM
    if ($failure) {
        throw $failure
    }
    Write-Host "VALIDATION PASS: $resultPath"
}

switch ($Verb) {
    'validate' { Invoke-Validation }
    'cleanup' {
        $certificates = Get-Certificates
        Remove-ManagedState
        Assert-Teardown | ConvertTo-Json -Compress | Write-Host
    }
    'status' {
        Assert-Host (Get-HostActiveEngine) | ConvertTo-Json -Depth 6 | Write-Host
    }
}
