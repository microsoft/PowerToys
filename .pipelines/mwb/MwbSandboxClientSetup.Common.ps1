# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Invoke-MwbClientCommand {
    param([string] $Executable, [string[]] $Arguments, [int] $TimeoutSeconds = 20, $ProcessTracker)

    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = $null
    try {
        $process = [Diagnostics.Process]::Start($start)
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timer = [Diagnostics.Stopwatch]::StartNew()
        do {
            if ($ProcessTracker) { Update-MwbClientOwnedProcesses $ProcessTracker }
            $exited = $process.WaitForExit(250)
        } while (-not $exited -and $timer.Elapsed.TotalSeconds -lt $TimeoutSeconds)
        $timedOut = -not $exited
        if ($timedOut) {
            # This handle belongs to this invocation. Never kill a provider/broker by name.
            $process.Kill()
            $null = $process.WaitForExit(2000)
        }
        $drained = [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr), 1000)
        [pscustomobject]@{
            ExitCode = if ($process.HasExited) { $process.ExitCode } else { $null }
            TimedOut = $timedOut
            OutputComplete = $drained
            Output = if ($drained -and -not $timedOut) { $stdout.GetAwaiter().GetResult() } else { '' }
        }
    }
    finally {
        if ($process) {
            # Descendants may inherit the pipes even after the command exits.
            $process.StandardOutput.BaseStream.Dispose()
            $process.StandardError.BaseStream.Dispose()
            $process.Dispose()
        }
    }
}

function Invoke-MwbClientPowerShell {
    param([string] $Command, [int] $TimeoutSeconds = 20, $ProcessTracker)

    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Command))
    Invoke-MwbClientCommand -Executable "$env:windir\System32\WindowsPowerShell\v1.0\powershell.exe" `
        -Arguments @('-NoLogo', '-NoProfile', '-NonInteractive', '-EncodedCommand', $encoded) `
        -TimeoutSeconds $TimeoutSeconds -ProcessTracker $ProcessTracker
}

function Get-MwbClientPackages {
    param([switch] $AllUsers, $ProcessTracker)

    $scope = if ($AllUsers) { '-AllUsers' } else { '' }
    $command = @'
$ErrorActionPreference = 'Stop'
try {
    Import-Module Appx -ErrorAction Stop
    $packages = @(Get-AppxPackage -Name MicrosoftWindows.WindowsSandbox SCOPE | Where-Object {
        $_.PackageFamilyName -ceq 'MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy'
    } | ForEach-Object {
        @{
            Family = [string]$_.PackageFamilyName; FullName = [string]$_.PackageFullName
            Version = [string]$_.Version; Healthy = ([string]$_.Status -ceq 'Ok')
            Location = [string]$_.InstallLocation; Signature = [string]$_.SignatureKind
            Development = [bool]$_.IsDevelopmentMode
        }
    })
    @{ Packages = $packages; ErrorHResult = $null } | ConvertTo-Json -Compress -Depth 4
    exit 0
} catch {
    @{ Packages = @(); ErrorHResult = ('0x{0:X8}' -f ($_.Exception.HResult -band 0xffffffffL)) } |
        ConvertTo-Json -Compress
    exit 1
}
'@
    $result = Invoke-MwbClientPowerShell ($command.Replace('SCOPE', $scope)) -ProcessTracker $ProcessTracker
    if ($result.TimedOut -or -not $result.OutputComplete) { throw 'PackageQueryTimeout' }
    $data = $result.Output | ConvertFrom-Json -AsHashtable
    if ($result.ExitCode -ne 0 -or $data.ErrorHResult) {
        $failure = [InvalidOperationException]::new('PackageQueryFailed')
        if ($data.ErrorHResult -cmatch '^0x[0-9A-F]{8}$') { $failure.Data['SafeHResult'] = $data.ErrorHResult }
        throw $failure
    }
    @($data.Packages)
}

function Get-MwbClientPackageDirectory {
    param([Collections.IDictionary] $Package)

    if ($Package.Family -cne 'MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy' -or
        $Package.FullName -cnotmatch '^MicrosoftWindows\.WindowsSandbox_\d+\.\d+\.\d+\.\d+_(?:x64|arm64|x86|neutral)_[A-Za-z0-9.~]*_cw5n1h2txyewy$' -or
        $Package.Signature -cnotin @('Store', 'System') -or $Package.Development -ne $false -or
        $Package.Healthy -ne $true) { throw 'UntrustedStagedPackage' }
    $expected = [IO.Path]::GetFullPath((Join-Path "$env:ProgramFiles\WindowsApps" $Package.FullName))
    if ([IO.Path]::GetFullPath([string]$Package.Location).TrimEnd('\') -ine $expected) {
        throw 'UntrustedPackageLocation'
    }
    Assert-MwbCiPlainPath $expected
    $expected
}

function Assert-MwbClientManifest {
    param([Collections.IDictionary] $Package)

    $expected = Get-MwbClientPackageDirectory $Package
    $manifest = Join-Path $expected 'AppxManifest.xml'
    Assert-MwbCiPlainPath $manifest
    # An OS-owned Store package, not an arbitrary extracted manifest or sideload.
    $trusted = @('S-1-5-18', 'S-1-5-32-544', 'S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464')
    foreach ($path in @("$env:ProgramFiles\WindowsApps", $expected, $manifest)) {
        $acl = Get-Acl -LiteralPath $path
        if ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -notin $trusted) {
            throw 'UntrustedPackageOwner'
        }
        $write = [Security.AccessControl.FileSystemRights]'Write,Delete,DeleteSubdirectoriesAndFiles,ChangePermissions,TakeOwnership'
        foreach ($rule in $acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
            if ($rule.AccessControlType -eq 'Allow' -and
                -not ($rule.PropagationFlags -band [Security.AccessControl.PropagationFlags]::InheritOnly) -and
                ($rule.FileSystemRights -band $write) -and
                $rule.IdentityReference.Value -notin $trusted) { throw 'WritablePackageLocation' }
        }
    }
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($manifest, $settings)
    try {
        $xml = [Xml.XmlDocument]::new()
        $xml.Load($reader)
        if ($xml.Package.Identity.Name -cne 'MicrosoftWindows.WindowsSandbox' -or
            $xml.Package.Identity.Version -cne $Package.Version -or
            $xml.Package.Identity.Publisher -cne 'CN=Microsoft Windows, O=Microsoft Corporation, L=Redmond, S=Washington, C=US') {
            throw 'ManifestIdentityMismatch'
        }
    }
    finally { $reader.Dispose() }
    $manifest
}

function ConvertTo-MwbClientVersion {
    param([string] $Output)

    # Match the runtime report's numeric version allowlist, not localized banner text.
    $match = [regex]::Match($Output,
        '^[ \t]*(?:(?:Windows Sandbox(?: CLI)?|wsb(?:\.exe)?)[ \t]+)?(?:version[ \t]*:?[ \t]*)?v?([0-9]{1,5}(?:\.[0-9]{1,5}){2,3})[ \t]*\r?$',
        [Text.RegularExpressions.RegexOptions]'Multiline,IgnoreCase,CultureInvariant')
    if ($match.Success) { "wsb $($match.Groups[1].Value)" }
}

function Get-MwbClientReadiness {
    param($ProcessTracker)

    $state = [ordered]@{
        Status = 'NotReady'; PackageCount = $null; Healthy = $false
        AliasExists = $false; AliasIsReparsePoint = $false
        VersionExitCode = $null; VersionTimedOut = $false; VersionOutputComplete = $false
        Version = $null; ErrorCode = $null; ErrorHResult = $null
    }
    try {
        $packages = @(Get-MwbClientPackages -ProcessTracker $ProcessTracker)
        if ($ProcessTracker) {
            foreach ($package in $packages) { Add-MwbClientPackageProcessPaths $ProcessTracker $package }
            Update-MwbClientOwnedProcesses $ProcessTracker
        }
        $state.PackageCount = $packages.Count
        $state.Healthy = $packages.Count -eq 1 -and $packages[0].Healthy -eq $true
        $alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\wsb.exe'
        $state.AliasExists = Test-Path -LiteralPath $alias -PathType Leaf
        $state.AliasIsReparsePoint = $state.AliasExists -and
            [bool]((Get-Item -LiteralPath $alias -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)
        if ($state.Healthy -and $state.AliasIsReparsePoint) {
            $version = Invoke-MwbClientCommand $alias @('--version') -TimeoutSeconds 15 -ProcessTracker $ProcessTracker
            $state.VersionExitCode = $version.ExitCode
            $state.VersionTimedOut = $version.TimedOut
            $state.VersionOutputComplete = $version.OutputComplete
            $state.Version = ConvertTo-MwbClientVersion $version.Output
            if (-not $version.TimedOut -and $version.OutputComplete -and $version.ExitCode -eq 0 -and $state.Version) {
                $state.Status = 'Ready'
            }
        }
    }
    catch {
        $state.Status = 'QueryFailed'
        $state.ErrorCode = 'ReadinessQueryFailed'
        $state.ErrorHResult = Get-MwbClientErrorHResult $_
    }
    $state
}

function Get-MwbClientDesktopProcesses {
    # Detection only: these names never grant ownership or authorize termination.
    @(Get-CimInstance Win32_Process -Filter (
        "Name='WindowsSandbox.exe' OR Name='WindowsSandboxClient.exe' OR Name='WindowsSandboxRemoteSession.exe' OR Name='vmmemWindowsSandbox' OR Name='vmmemWindowsSandbox.exe'") `
        -Property Name, ProcessId, ParentProcessId, SessionId, CreationDate -OperationTimeoutSec 2 -ErrorAction Stop |
        Select-Object Name, ProcessId, ParentProcessId, SessionId, CreationDate)
}

function Initialize-MwbClientProcessImage {
    if (-not ('MwbClientSetup.ProcessImage' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace MwbClientSetup
{
    public static class ProcessImage
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(
            SafeProcessHandle process, uint flags, StringBuilder path, ref uint size);

        public static string GetPath(SafeProcessHandle process)
        {
            var path = new StringBuilder(32768);
            uint size = (uint)path.Capacity;
            if (!QueryFullProcessImageName(process, 0, path, ref size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return path.ToString();
        }
    }
}
'@
    }
}

function Get-MwbClientProcessImagePath {
    param($Process)

    Initialize-MwbClientProcessImage
    [MwbClientSetup.ProcessImage]::GetPath($Process.SafeHandle)
}

function Open-MwbClientProcess {
    param([int] $ProcessId)

    $process = [Diagnostics.Process]::GetProcessById($ProcessId)
    try {
        # Force a retained kernel handle before reading identity or allowing PID reuse.
        $null = $process.SafeHandle
        $process
    }
    catch {
        $process.Dispose()
        throw
    }
}

function Get-MwbClientSessionId {
    $process = [Diagnostics.Process]::GetCurrentProcess()
    try { $process.SessionId } finally { $process.Dispose() }
}

function New-MwbClientProcessTracker {
    param($Process, [string] $PackageProofPath, [guid] $RunId = [guid]::Empty)

    $path = Get-MwbClientProcessImagePath $Process
    if ($path -ine (Join-Path $env:windir 'System32\WindowsSandbox.exe')) { throw 'UntrustedInboxLauncher' }
    @{
        Records = [Collections.Generic.List[object]]::new()
        Root = [pscustomobject]@{
            Process = $Process; ProcessId = $Process.Id; ParentProcessId = $PID
            Name = 'WindowsSandbox.exe'; Path = $path
            # Process.Start inherits this worker's session. SessionId can no longer
            # be queried by PID after the short-lived inbox launcher has exited.
            StartTime = $Process.StartTime.ToUniversalTime(); SessionId = Get-MwbClientSessionId
        }
        TrustedPaths = @{}; Packages = @{}; LastScan = [datetime]::MinValue
        TrackingError = $null; TrackingErrorHResult = $null
        PackageProofPath = $PackageProofPath; RunId = $RunId.ToString(); PendingPackage = $null
    }
}

function Set-MwbClientTrackingError {
    param($Tracker, [string] $Code, [Management.Automation.ErrorRecord] $ErrorRecord)

    $Tracker.TrackingError = $Code
    $Tracker.TrackingErrorHResult = Get-MwbClientErrorHResult $ErrorRecord
}

function Assert-MwbClientPackageProof {
    param([Collections.IDictionary] $Package, [string] $ProofPath, [string] $RunId)

    $expected = Get-MwbClientPackageDirectory $Package
    Assert-MwbClientProtectedDirectory $ProofPath
    if ((Get-Item -LiteralPath $ProofPath).Length -gt 16384) { throw 'InvalidPackageProof' }
    $proof = Get-Content -LiteralPath $ProofPath -Raw | ConvertFrom-Json -AsHashtable
    if ($proof.RunId -cne $RunId -or $proof.FullName -cne $Package.FullName -or
        $proof.Version -cne $Package.Version -or $proof.Location -ine $expected) { throw 'InvalidPackageProof' }
    Join-Path $expected 'AppxManifest.xml'
}

function Add-MwbClientPackageProcessPaths {
    param($Tracker, [Collections.IDictionary] $Package)

    if ($Tracker.Packages.ContainsKey([string]$Package.FullName)) { return }
    $Tracker.PendingPackage = $Package
    try {
        if ($Tracker.PackageProofPath) {
            $null = Assert-MwbClientPackageProof $Package $Tracker.PackageProofPath $Tracker.RunId
        }
        else {
            $null = Assert-MwbClientManifest $Package
        }
        foreach ($name in @('WindowsSandbox.exe', 'WindowsSandboxClient.exe', 'WindowsSandboxRemoteSession.exe')) {
            $path = Join-Path $Package.Location $name
            Assert-MwbCiPlainPath $path
            if (Test-Path -LiteralPath $path -PathType Leaf) { $Tracker.TrustedPaths[$path] = $true }
        }
        $Tracker.Packages[$Package.FullName] = $true
        $Tracker.PendingPackage = $null
    }
    catch {
        Set-MwbClientTrackingError $Tracker 'PackageValidationFailed' $_
    }
}

function Test-MwbClientProcessPath {
    param($Tracker, [string] $Path, [string] $Name)

    if ($Name -inotmatch '^WindowsSandbox(?:Client|RemoteSession)?\.exe$' -or
        [IO.Path]::GetFileName($Path) -ine $Name) { return $false }
    if ($Tracker.TrustedPaths.ContainsKey($Path)) { return $true }
    if ($Path -ine (Join-Path "$env:windir\System32" $Name)) { return $false }
    try {
        Assert-MwbCiPlainPath $Path
        $signature = Get-AuthenticodeSignature -LiteralPath $Path
        if ($signature.Status -ne 'Valid' -or
            $signature.SignerCertificate.Subject -notmatch '(^|, )O=Microsoft Corporation(,|$)') { return $false }
        $Tracker.TrustedPaths[$Path] = $true
        $true
    }
    catch {
        Set-MwbClientTrackingError $Tracker 'ProcessPathValidationFailed' $_
        $false
    }
}

function Test-MwbClientParentLifetime {
    param($Parent, [datetime] $ChildStartTime)

    if ($ChildStartTime -lt $Parent.StartTime) { return $false }
    # A dead parent's PID alone is insufficient: a later process can reuse that PID.
    if ($Parent.Process.HasExited -and $ChildStartTime -gt $Parent.Process.ExitTime.ToUniversalTime()) { return $false }
    $true
}

function Update-MwbClientOwnedProcesses {
    param($Tracker, [switch] $Force)

    if (-not $Tracker -or (-not $Force -and ([datetime]::UtcNow - $Tracker.LastScan).TotalMilliseconds -lt 500)) { return }
    $Tracker.LastScan = [datetime]::UtcNow
    if ($Tracker.PendingPackage) { Add-MwbClientPackageProcessPaths $Tracker $Tracker.PendingPackage }
    try {
        $snapshot = @(Get-MwbClientDesktopProcesses)
        do {
            $added = $false
            foreach ($candidate in $snapshot) {
                if ($candidate.Name -inotmatch '^WindowsSandbox(?:Client|RemoteSession)?\.exe$' -or
                    $candidate.SessionId -ne $Tracker.Root.SessionId -or
                    $candidate.ProcessId -eq $Tracker.Root.ProcessId -or
                    @($Tracker.Records | Where-Object ProcessId -EQ $candidate.ProcessId).Count -or
                    $Tracker.Records.Count -ge 32) { continue }
                $parents = @(@($Tracker.Root) + @($Tracker.Records) |
                    Where-Object ProcessId -EQ $candidate.ParentProcessId)
                if ($parents.Count -ne 1) { continue }
                $process = $null
                try {
                    $process = Open-MwbClientProcess ([int]$candidate.ProcessId)
                    $started = $process.StartTime.ToUniversalTime()
                    # CIM timestamps have microsecond precision; process times retain 100 ns.
                    $difference = ($started - $candidate.CreationDate.ToUniversalTime()).Ticks
                    if ($difference -lt 0 -or $difference -ge 10 -or
                        $process.SessionId -ne $Tracker.Root.SessionId -or
                        -not (Test-MwbClientParentLifetime $parents[0] $started)) { continue }
                    $path = Get-MwbClientProcessImagePath $process
                    if (-not (Test-MwbClientProcessPath $Tracker $path $candidate.Name)) { continue }
                    $Tracker.Records.Add([pscustomobject]@{
                        Process = $process; ProcessId = $candidate.ProcessId; ParentProcessId = $candidate.ParentProcessId
                        Name = $candidate.Name; Path = $path; StartTime = $started; SessionId = $process.SessionId
                    })
                    $process = $null
                    $added = $true
                }
                catch {
                    Set-MwbClientTrackingError $Tracker 'ProcessIdentityQueryFailed' $_
                }
                finally { if ($process) { $process.Dispose() } }
            }
        } while ($added)
    }
    catch {
        Set-MwbClientTrackingError $Tracker 'ProcessSnapshotFailed' $_
    }
}

function Get-MwbClientRemainingProcesses {
    param($Tracker)

    @(foreach ($candidate in @(Get-MwbClientDesktopProcesses) | Select-Object -First 64) {
        $owned = $false
        $started = if ($candidate.CreationDate) { $candidate.CreationDate.ToUniversalTime() } else { $null }
        if ($Tracker -and $started) {
            foreach ($record in @($Tracker.Root) + @($Tracker.Records)) {
                $difference = ($record.StartTime - $started).Ticks
                if ($record.ProcessId -eq $candidate.ProcessId -and $record.SessionId -eq $candidate.SessionId -and
                    $record.ParentProcessId -eq $candidate.ParentProcessId -and $record.Name -ieq $candidate.Name -and
                    $difference -ge 0 -and $difference -lt 10 -and -not $record.Process.HasExited) { $owned = $true }
            }
        }
        [ordered]@{
            Name = [string]$candidate.Name; PID = [long]$candidate.ProcessId
            ParentPID = [long]$candidate.ParentProcessId; SessionId = [long]$candidate.SessionId
            StartTime = if ($started) { $started.ToString('o') } else { $null }
            VerifiedOwned = $owned
        }
    })
}

function Assert-MwbClientEmptyProvider {
    param($ProcessTracker)

    $alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\wsb.exe'
    $result = Invoke-MwbClientCommand $alias @('list', '--raw') -TimeoutSeconds 15 -ProcessTracker $ProcessTracker
    if ($result.TimedOut -or -not $result.OutputComplete -or $result.ExitCode -ne 0) { throw 'SandboxInventoryFailed' }
    $inventory = $result.Output | ConvertFrom-Json -AsHashtable
    if (-not $inventory.Contains('WindowsSandboxEnvironments') -or
        $inventory.WindowsSandboxEnvironments -isnot [array]) { throw 'SandboxInventorySchemaMismatch' }
    if ($inventory.WindowsSandboxEnvironments.Count) { throw 'ExistingSandboxInstance' }
}

function Assert-MwbClientNoInstances {
    param([switch] $ProbeProvider)

    if (@(Get-MwbClientDesktopProcesses).Count) { throw 'ExistingSandboxDesktop' }
    if ($ProbeProvider) { Assert-MwbClientEmptyProvider }
}

function Get-MwbClientInboxLauncher {
    $executable = Join-Path $env:windir 'System32\WindowsSandbox.exe'
    Assert-MwbCiPlainPath $executable
    $signature = Get-AuthenticodeSignature -LiteralPath $executable
    if ($signature.Status -ne 'Valid' -or
        $signature.SignerCertificate.Subject -notmatch '(^|, )O=Microsoft Corporation(,|$)') {
        throw 'UntrustedInboxLauncher'
    }
    # Use only the installed, documented .wsb association, not guessed install switches.
    $progId = (Get-Item -LiteralPath 'Registry::HKEY_CLASSES_ROOT\.wsb').GetValue('')
    # Modern Windows can leave .wsb's default unset while retaining this OS ProgID.
    if ([string]::IsNullOrWhiteSpace($progId)) { $progId = 'Windows.Sandbox' }
    if ($progId -notmatch '^[A-Za-z0-9.]+$') { throw 'SandboxAssociationMissing' }
    $command = [Environment]::ExpandEnvironmentVariables(
        (Get-Item -LiteralPath "Registry::HKEY_CLASSES_ROOT\$progId\shell\open\command").GetValue(''))
    if ($command -ine "`"$executable`" `"%1`"" -and $command -ine "$executable `"%1`"") {
        throw 'SandboxAssociationMismatch'
    }
    $executable
}

function Start-MwbClientFirstLaunch {
    param([string] $Directory, [guid] $RunId)

    $executable = Get-MwbClientInboxLauncher
    Initialize-MwbClientProcessImage
    $configuration = Join-Path $Directory 'client-setup.wsb'
    # Only this fresh guest executes shutdown. No host process/instance is adopted.
    $xml = @"
<Configuration>
  <Networking>Disable</Networking>
  <vGPU>Disable</vGPU>
  <AudioInput>Disable</AudioInput>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <MappedFolders><MappedFolder>
    <HostFolder>$([Security.SecurityElement]::Escape($Directory))</HostFolder>
    <SandboxFolder>C:\MwbClientSetup</SandboxFolder><ReadOnly>false</ReadOnly>
  </MappedFolder></MappedFolders>
  <LogonCommand><Command>cmd.exe /d /c "echo $RunId&gt;C:\MwbClientSetup\guest-started.txt &amp; shutdown.exe /s /f /t 0"</Command></LogonCommand>
</Configuration>
"@
    Set-Content -LiteralPath $configuration -Value $xml -Encoding utf8
    Assert-MwbClientNoInstances
    $start = [Diagnostics.ProcessStartInfo]::new($executable)
    $start.UseShellExecute = $false
    $start.ArgumentList.Add($configuration)
    [Diagnostics.Process]::Start($start)
}

function Register-MwbClientManifest {
    param([Collections.IDictionary] $Package, [string] $PackageProofPath, [guid] $RunId = [guid]::Empty)

    $manifest = if ($PackageProofPath) {
        Assert-MwbClientPackageProof $Package $PackageProofPath $RunId.ToString()
    } else {
        Assert-MwbClientManifest $Package
    }
    $literal = $manifest.Replace("'", "''")
    $result = Invoke-MwbClientPowerShell -TimeoutSeconds 120 -Command @"
`$ErrorActionPreference = 'Stop'
try {
    Import-Module Appx -ErrorAction Stop
    Add-AppxPackage -Register '$literal' -DisableDevelopmentMode -ErrorAction Stop | Out-Null
    exit 0
} catch {
    @{ ErrorHResult = ('0x{0:X8}' -f (`$_.Exception.HResult -band 0xffffffffL)) } | ConvertTo-Json -Compress
    exit 1
}
"@
    if ($result.TimedOut) { throw 'RegistrationTimeout' }
    if (-not $result.OutputComplete -or $result.ExitCode -ne 0) {
        $failure = [InvalidOperationException]::new('RegistrationFailed')
        try {
            $data = $result.Output | ConvertFrom-Json -AsHashtable
            if ($data.ErrorHResult -cmatch '^0x[0-9A-F]{8}$') { $failure.Data['SafeHResult'] = $data.ErrorHResult }
        }
        catch { throw [InvalidOperationException]::new('RegistrationResponseInvalid') }
        throw $failure
    }
}

function Stop-MwbClientOwnedProcesses {
    param($Tracker)

    for ($index = $Tracker.Records.Count - 1; $index -ge 0; $index--) {
        $record = $Tracker.Records[$index]
        try {
            if (-not $record.Process.HasExited -and
                $record.Process.StartTime.ToUniversalTime() -eq $record.StartTime -and
                $record.Process.SessionId -eq $record.SessionId -and
                (Get-MwbClientProcessImagePath $record.Process) -ieq $record.Path -and
                (Test-MwbClientProcessPath $Tracker $record.Path $record.Name)) {
                # No PID reopen or tree kill: only this retained, verified descendant handle.
                $record.Process.Kill()
            }
        }
        catch {
            Set-MwbClientTrackingError $Tracker 'OwnedProcessStopFailed' $_
        }
    }
}

function Complete-MwbClientFirstLaunch {
    param($Process, [string] $Directory, [guid] $RunId, [int] $TimeoutSeconds = 60, $ProcessTracker)

    $timer = [Diagnostics.Stopwatch]::StartNew()
    $marker = Join-Path $Directory 'guest-started.txt'
    $guestObserved = $false
    $inventoryFailure = $null
    $markerMismatch = $false
    do {
        Update-MwbClientOwnedProcesses $ProcessTracker
        if (Test-Path -LiteralPath $marker -PathType Leaf) {
            Assert-MwbCiPlainPath $marker
            if ((Get-Item -LiteralPath $marker).Length -gt 128 -or
                (Get-Content -LiteralPath $marker -Raw).Trim() -cne $RunId.ToString()) {
                $markerMismatch = $true
                break
            }
            $guestObserved = $true
        }
        if ($guestObserved -and $ProcessTracker -and
            @($ProcessTracker.Records | Where-Object { -not $_.Process.HasExited }).Count) {
            try {
                # Guest acknowledgement and an empty provider precede any client termination.
                # Neither a new process nor an empty provider alone proves process ownership.
                Assert-MwbClientEmptyProvider -ProcessTracker $ProcessTracker
                Stop-MwbClientOwnedProcesses $ProcessTracker
            }
            catch { $inventoryFailure = $_ }
        }
        if ($guestObserved -and $Process.HasExited) {
            try {
                Assert-MwbClientNoInstances -ProbeProvider
                return [ordered]@{ Status = 'Passed'; GuestObserved = $guestObserved; LauncherStopped = $true }
            }
            catch { $inventoryFailure = $_ }
        }
        Start-Sleep -Milliseconds 500
    } while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds)
    if (-not $markerMismatch -and -not $Process.HasExited) {
        $Process.Kill()
        $null = $Process.WaitForExit(2000)
    }
    if ($guestObserved -and -not $markerMismatch -and $Process.HasExited) {
        try {
            Assert-MwbClientNoInstances -ProbeProvider
            return [ordered]@{ Status = 'Passed'; GuestObserved = $guestObserved; LauncherStopped = $true }
        }
        catch { $inventoryFailure = $_ }
    }
    $failure = [InvalidOperationException]::new($(if ($markerMismatch) { 'GuestMarkerMismatch' } else { 'FirstLaunchCleanupUnconfirmed' }))
    $failure.Data['GuestObserved'] = $guestObserved
    $failure.Data['LauncherStopped'] = $Process.HasExited
    if ($inventoryFailure) {
        $failure.Data['SafeHResult'] = Get-MwbClientErrorHResult $inventoryFailure
        $failure.Data['InventoryError'] = if ($inventoryFailure.Exception.Message -cin @(
            'ExistingSandboxDesktop', 'ExistingSandboxInstance', 'SandboxInventoryFailed', 'SandboxInventorySchemaMismatch')) {
            $inventoryFailure.Exception.Message
        } else { 'SandboxInventoryFailed' }
    }
    try { $failure.Data['RemainingProcesses'] = @(Get-MwbClientRemainingProcesses $ProcessTracker) }
    catch { $failure.Data['InventoryError'] = 'SandboxInventoryFailed' }
    throw $failure
}

function Save-MwbClientFailureDesktop {
    param([string] $Directory)

    $bitmap = $null
    $graphics = $null
    try {
        Add-Type -AssemblyName System.Windows.Forms
        $bounds = [Windows.Forms.SystemInformation]::VirtualScreen
        $bitmap = [Drawing.Bitmap]::new($bounds.Width, $bounds.Height)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $graphics.CopyFromScreen($bounds.Location, [Drawing.Point]::Empty, $bounds.Size)
        $bitmap.Save((Join-Path $Directory 'client-setup-failure.png'), [Drawing.Imaging.ImageFormat]::Png)
        @{ Status = 'Captured' }
    }
    catch { @{ Status = 'Failed'; ErrorHResult = Get-MwbClientErrorHResult $_ } }
    finally {
        if ($graphics) { $graphics.Dispose() }
        if ($bitmap) { $bitmap.Dispose() }
    }
}

function Invoke-MwbClientSetupWorker {
    param([Collections.IDictionary] $Request, [Collections.IDictionary] $Report, [string] $Directory)

    $launcher = $null
    $processTracker = $null
    $firstLaunchTimer = $null
    try {
        $Report.Before = Get-MwbClientReadiness
        $Report.After = $Report.Before
        Save-MwbClientWorkerReport $Report $Directory
        if ($Report.Before.Status -eq 'Ready') {
            $Report.Action = 'None'
            $Report.Status = 'Ready'
            return
        }
        if ($Report.Before.Status -eq 'QueryFailed') { throw 'InitialReadinessQueryFailed' }
        if ($Request.PackageQueryFailed) { throw 'StagedPackageQueryFailed' }
        Assert-MwbClientNoInstances -ProbeProvider:($Report.Before.Healthy -and $Report.Before.AliasIsReparsePoint)
        if ($Request.Package) {
            $Report.Action = 'RegisterStagedPackage'
            Save-MwbClientWorkerReport $Report $Directory
            Register-MwbClientManifest $Request.Package `
                -PackageProofPath (Join-Path $PSScriptRoot 'package-trust.json') -RunId ([guid]$Request.RunId)
        }
        else {
            $Report.Action = 'InboxFirstLaunch'
            $Report.Cleanup.Status = 'Pending'
            Save-MwbClientWorkerReport $Report $Directory
            $firstLaunchTimer = [Diagnostics.Stopwatch]::StartNew()
            $launcher = Start-MwbClientFirstLaunch $Directory ([guid]$Request.RunId)
            $processTracker = New-MwbClientProcessTracker $launcher `
                -PackageProofPath (Join-Path $PSScriptRoot 'package-trust.json') -RunId ([guid]$Request.RunId)
            Update-MwbClientOwnedProcesses $processTracker -Force
        }
        $timer = [Diagnostics.Stopwatch]::StartNew()
        do {
            $Report.After = Get-MwbClientReadiness -ProcessTracker $processTracker
            Save-MwbClientWorkerReport $Report $Directory
            if ($Report.After.Status -eq 'Ready') { break }
            for ($poll = 0; $poll -lt 10; $poll++) {
                Update-MwbClientOwnedProcesses $processTracker
                Start-Sleep -Milliseconds 500
            }
        } while ($timer.Elapsed.TotalSeconds -lt $Request.InstallTimeoutSeconds)
        if ($Report.After.Status -ne 'Ready') { throw 'ClientSetupDeadlineExceeded' }
        $Report.Status = 'Ready'
    }
    catch {
        $Report.Status = 'Failed'
        $allowed = @('InitialReadinessQueryFailed', 'StagedPackageQueryFailed', 'ExistingSandboxDesktop', 'ExistingSandboxInstance',
            'SandboxInventoryFailed', 'SandboxInventorySchemaMismatch', 'UntrustedStagedPackage',
            'UntrustedPackageLocation', 'UntrustedPackageOwner', 'WritablePackageLocation', 'ManifestIdentityMismatch',
            'UntrustedInboxLauncher', 'SandboxAssociationMissing', 'SandboxAssociationMismatch',
            'RegistrationTimeout', 'RegistrationFailed', 'RegistrationResponseInvalid', 'ClientSetupDeadlineExceeded')
        $Report.ErrorCode = if ($_.Exception.Message -cin $allowed) { $_.Exception.Message } else { 'ClientSetupFailed' }
        $Report.ErrorHResult = Get-MwbClientErrorHResult $_
    }
    finally {
        if ($launcher) {
            try {
                # Registration can finish before the cold guest reaches its self-shutdown
                # logon command. Share an eight-minute launch budget instead of giving
                # early registration only sixty seconds to finish guest startup.
                $remaining = [Math]::Max(1, [int][Math]::Ceiling(480 - $firstLaunchTimer.Elapsed.TotalSeconds))
                $Report.Cleanup = Complete-MwbClientFirstLaunch $launcher $Directory ([guid]$Request.RunId) $remaining $processTracker
            }
            catch {
                $cleanupError = $_
                $remainingProcesses = $cleanupError.Exception.Data['RemainingProcesses']
                if ($null -eq $remainingProcesses) {
                    try { $remainingProcesses = @(Get-MwbClientRemainingProcesses $processTracker) }
                    catch { $cleanupError.Exception.Data['InventoryError'] = 'SandboxInventoryFailed' }
                }
                $Report.Cleanup = [ordered]@{
                    Status = 'Failed'
                    ErrorCode = if ($cleanupError.Exception.Message -ceq 'GuestMarkerMismatch') { 'GuestMarkerMismatch' } else { 'FirstLaunchCleanupUnconfirmed' }
                    ErrorHResult = Get-MwbClientErrorHResult $cleanupError
                    GuestObserved = $cleanupError.Exception.Data['GuestObserved']
                    LauncherStopped = $cleanupError.Exception.Data['LauncherStopped']
                    InventoryError = $cleanupError.Exception.Data['InventoryError']
                    RemainingProcesses = $remainingProcesses
                }
                $Report.Status = 'Failed'
            }
            finally {
                if ($processTracker) {
                    $Report.Cleanup.TrackingError = $processTracker['TrackingError']
                    $Report.Cleanup.TrackingErrorHResult = $processTracker['TrackingErrorHResult']
                    foreach ($record in $processTracker.Records) { $record.Process.Dispose() }
                }
                $launcher.Dispose()
            }
        }
        elseif ($Report.Cleanup.Status -eq 'Pending') { $Report.Cleanup.Status = 'NotStarted' }
        if ($Report.Action -ne 'None') {
            $Report.After = Get-MwbClientReadiness
            if ($Report.Status -eq 'Ready' -and $Report.After.Status -ne 'Ready') {
                $Report.Status = 'Failed'
                $Report.ErrorCode = 'FinalReadinessFailed'
            }
        }
        if ($Report.Status -eq 'Failed') {
            $Report.FailureCapture = Save-MwbClientFailureDesktop $Directory
        }
        Save-MwbClientWorkerReport $Report $Directory
    }
}

function Save-MwbClientWorkerReport {
    param([Collections.IDictionary] $Report, [string] $Directory)

    $path = Join-Path $Directory 'worker-report.json'
    Assert-MwbCiPlainPath $path
    Assert-MwbCiPlainPath "$path.new"
    $Report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$path.new" -Encoding utf8
    Move-Item -LiteralPath "$path.new" -Destination $path -Force
}

function Get-MwbClientErrorHResult {
    param([Management.Automation.ErrorRecord] $ErrorRecord)

    $value = $ErrorRecord.Exception.Data['SafeHResult']
    if ($value -is [string] -and $value -cmatch '^0x[0-9A-F]{8}$') { return $value }
    '0x{0:X8}' -f ($ErrorRecord.Exception.HResult -band 0xffffffffL)
}

function ConvertTo-MwbPublicClientSetup {
    param([Collections.IDictionary] $Source, [string] $RunId, [string] $UserSid)

    if ($Source.RunId -cne $RunId -or $Source.UserSid -cne $UserSid -or
        $Source.Status -cnotin @('Ready', 'Failed') -or $Source.IsElevated -ne $false -or
        ($Source.SessionId -isnot [long] -and $Source.SessionId -isnot [int]) -or $Source.SessionId -le 0) {
        throw 'InvalidClientSetupReport'
    }
    $result = [ordered]@{
        SchemaVersion = 1; RunId = $RunId; UserSid = $UserSid
        IsElevated = $false; SessionId = $Source.SessionId; Status = $Source.Status
    }
    $rules = @{
        Action = '^(None|RegisterStagedPackage|InboxFirstLaunch)$'
        ErrorCode = '^[A-Za-z][A-Za-z0-9]{0,63}$'; ErrorHResult = '^0x[0-9A-F]{8}$'
        Status = '^(Ready|NotReady|QueryFailed|NotChecked|NotStarted|Passed|Failed|Pending)$'
        Version = '^wsb \d{1,5}(?:\.\d{1,5}){2,3}$'
    }
    $errorCodes = @('ReadinessQueryFailed', 'InitialReadinessQueryFailed', 'StagedPackageQueryFailed',
        'ExistingSandboxDesktop', 'ExistingSandboxInstance', 'SandboxInventoryFailed', 'SandboxInventorySchemaMismatch',
        'UntrustedStagedPackage', 'UntrustedPackageLocation', 'UntrustedPackageOwner', 'WritablePackageLocation',
        'ManifestIdentityMismatch', 'UntrustedInboxLauncher', 'SandboxAssociationMissing', 'SandboxAssociationMismatch',
        'RegistrationTimeout', 'RegistrationFailed', 'RegistrationResponseInvalid', 'ClientSetupDeadlineExceeded', 'ClientSetupFailed',
        'FirstLaunchCleanupUnconfirmed', 'GuestMarkerMismatch', 'SetupWorkerFailed', 'FinalReadinessFailed')
    foreach ($name in @('Action', 'ErrorCode', 'ErrorHResult', 'Before', 'After', 'Cleanup', 'FailureCapture')) {
        $value = $Source[$name]
        if ($name -eq 'FailureCapture' -and $null -eq $value) { continue }
        if ($name -in @('Before', 'After', 'Cleanup', 'FailureCapture')) {
            $copy = [ordered]@{}
            if ($value -isnot [Collections.IDictionary]) { throw 'InvalidClientSetupReport' }
            foreach ($field in @('Status', 'PackageCount', 'Healthy', 'AliasExists', 'AliasIsReparsePoint',
                'VersionExitCode', 'VersionTimedOut', 'VersionOutputComplete', 'Version', 'ErrorCode',
                'ErrorHResult', 'GuestObserved', 'LauncherStopped', 'InventoryError', 'TrackingError', 'TrackingErrorHResult')) {
                if (-not $value.Contains($field)) { continue }
                $item = $value[$field]
                if ($null -ne $item) {
                    if ($field -eq 'ErrorCode' -and $item -cnotin $errorCodes) { throw 'InvalidClientSetupReport' }
                    if ($field -eq 'InventoryError') {
                        if ($item -cnotin @('ExistingSandboxDesktop','ExistingSandboxInstance','SandboxInventoryFailed','SandboxInventorySchemaMismatch')) {
                            throw 'InvalidClientSetupReport'
                        }
                    }
                    elseif ($field -eq 'TrackingError') {
                        if ($item -cnotin @('PackageValidationFailed','ProcessPathValidationFailed','ProcessIdentityQueryFailed',
                            'ProcessSnapshotFailed','OwnedProcessStopFailed')) { throw 'InvalidClientSetupReport' }
                    }
                    elseif ($field -eq 'TrackingErrorHResult') {
                        if ($item -isnot [string] -or $item -cnotmatch '^0x[0-9A-F]{8}$') { throw 'InvalidClientSetupReport' }
                    }
                    elseif ($field -eq 'Status' -and $name -eq 'FailureCapture') {
                        if ($item -cnotin @('Captured','Failed')) { throw 'InvalidClientSetupReport' }
                    }
                    elseif ($rules.ContainsKey($field)) {
                        if ($item -isnot [string] -or $item -cnotmatch $rules[$field]) { throw 'InvalidClientSetupReport' }
                    }
                    elseif ($field -in @('PackageCount', 'VersionExitCode')) {
                        if ($item -isnot [int] -and $item -isnot [long]) { throw 'InvalidClientSetupReport' }
                    }
                    elseif ($item -isnot [bool]) { throw 'InvalidClientSetupReport' }
                }
                $copy[$field] = $item
            }
            if ($name -eq 'Cleanup' -and $null -ne $value.RemainingProcesses) {
                $copy.RemainingProcesses = @(ConvertTo-MwbPublicClientProcesses $value.RemainingProcesses)
            }
            $result[$name] = $copy
        }
        else {
            if ($name -eq 'ErrorCode' -and $null -ne $value -and $value -cnotin $errorCodes) {
                throw 'InvalidClientSetupReport'
            }
            if ($null -ne $value -and ($value -isnot [string] -or $value -cnotmatch $rules[$name])) {
                throw 'InvalidClientSetupReport'
            }
            $result[$name] = $value
        }
    }
    $result
}

function ConvertTo-MwbPublicClientProcesses {
    param($Processes)

    if ($Processes -isnot [array] -or $Processes.Count -gt 64) { throw 'InvalidClientSetupReport' }
    foreach ($process in $Processes) {
        if ($process -isnot [Collections.IDictionary] -or
            $process.Name -isnot [string] -or $process.Name -inotmatch '^(WindowsSandbox(?:Client|RemoteSession)?\.exe|vmmemWindowsSandbox(?:\.exe)?)$' -or
            $process.VerifiedOwned -isnot [bool]) { throw 'InvalidClientSetupReport' }
        $copy = [ordered]@{ Name = $process.Name }
        foreach ($field in @('PID', 'ParentPID', 'SessionId')) {
            $value = $process[$field]
            if (($value -isnot [int] -and $value -isnot [long]) -or $value -lt 0 -or $value -gt [uint32]::MaxValue) {
                throw 'InvalidClientSetupReport'
            }
            $copy[$field] = $value
        }
        $started = $process.StartTime
        if ($started -is [datetime]) { $started = $started.ToUniversalTime().ToString('o') }
        if ($null -ne $started -and ($started -isnot [string] -or
            $started -cnotmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$')) { throw 'InvalidClientSetupReport' }
        $copy.StartTime = $started
        $copy.VerifiedOwned = $process.VerifiedOwned
        $copy
    }
}
