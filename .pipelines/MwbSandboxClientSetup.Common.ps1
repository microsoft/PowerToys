# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Invoke-MwbClientCommand {
    param([string] $Executable, [string[]] $Arguments, [int] $TimeoutSeconds = 20)

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
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
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
    param([string] $Command, [int] $TimeoutSeconds = 20)

    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Command))
    Invoke-MwbClientCommand -Executable "$env:windir\System32\WindowsPowerShell\v1.0\powershell.exe" `
        -Arguments @('-NoLogo', '-NoProfile', '-NonInteractive', '-EncodedCommand', $encoded) `
        -TimeoutSeconds $TimeoutSeconds
}

function Get-MwbClientPackages {
    param([switch] $AllUsers)

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
    $result = Invoke-MwbClientPowerShell ($command.Replace('SCOPE', $scope))
    if ($result.TimedOut -or -not $result.OutputComplete) { throw 'PackageQueryTimeout' }
    $data = $result.Output | ConvertFrom-Json -AsHashtable
    if ($result.ExitCode -ne 0 -or $data.ErrorHResult) {
        $failure = [InvalidOperationException]::new('PackageQueryFailed')
        if ($data.ErrorHResult -cmatch '^0x[0-9A-F]{8}$') { $failure.Data['SafeHResult'] = $data.ErrorHResult }
        throw $failure
    }
    @($data.Packages)
}

function Assert-MwbClientManifest {
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
    $state = [ordered]@{
        Status = 'NotReady'; PackageCount = $null; Healthy = $false
        AliasExists = $false; AliasIsReparsePoint = $false
        VersionExitCode = $null; VersionTimedOut = $false; VersionOutputComplete = $false
        Version = $null; ErrorCode = $null; ErrorHResult = $null
    }
    try {
        $packages = @(Get-MwbClientPackages)
        $state.PackageCount = $packages.Count
        $state.Healthy = $packages.Count -eq 1 -and $packages[0].Healthy -eq $true
        $alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\wsb.exe'
        $state.AliasExists = Test-Path -LiteralPath $alias -PathType Leaf
        $state.AliasIsReparsePoint = $state.AliasExists -and
            [bool]((Get-Item -LiteralPath $alias -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)
        if ($state.Healthy -and $state.AliasIsReparsePoint) {
            $version = Invoke-MwbClientCommand $alias @('--version') -TimeoutSeconds 15
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
        -Property ProcessId, CreationDate -OperationTimeoutSec 10 -ErrorAction Stop |
        Select-Object ProcessId, CreationDate)
}

function Assert-MwbClientNoInstances {
    param([switch] $ProbeProvider)

    if (@(Get-MwbClientDesktopProcesses).Count) { throw 'ExistingSandboxDesktop' }
    if ($ProbeProvider) {
        $alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\wsb.exe'
        $result = Invoke-MwbClientCommand $alias @('list', '--raw') -TimeoutSeconds 15
        if ($result.TimedOut -or -not $result.OutputComplete -or $result.ExitCode -ne 0) { throw 'SandboxInventoryFailed' }
        $inventory = $result.Output | ConvertFrom-Json -AsHashtable
        if (-not $inventory.Contains('WindowsSandboxEnvironments') -or
            $inventory.WindowsSandboxEnvironments -isnot [array]) { throw 'SandboxInventorySchemaMismatch' }
        if ($inventory.WindowsSandboxEnvironments.Count) { throw 'ExistingSandboxInstance' }
    }
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
    param([Collections.IDictionary] $Package)

    $manifest = Assert-MwbClientManifest $Package
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

function Complete-MwbClientFirstLaunch {
    param($Process, [string] $Directory, [guid] $RunId, [int] $TimeoutSeconds = 60)

    $timer = [Diagnostics.Stopwatch]::StartNew()
    $marker = Join-Path $Directory 'guest-started.txt'
    $guestObserved = $false
    $inventoryFailure = $null
    do {
        if (Test-Path -LiteralPath $marker -PathType Leaf) {
            Assert-MwbCiPlainPath $marker
            if ((Get-Item -LiteralPath $marker).Length -gt 128 -or
                (Get-Content -LiteralPath $marker -Raw).Trim() -cne $RunId.ToString()) { throw 'GuestMarkerMismatch' }
            $guestObserved = $true
        }
        if ($Process.HasExited) {
            try {
                Assert-MwbClientNoInstances -ProbeProvider
                return [ordered]@{ Status = 'Passed'; GuestObserved = $guestObserved; LauncherStopped = $true }
            }
            catch { $inventoryFailure = $_ }
        }
        Start-Sleep -Seconds 2
    } while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds)
    # Do not stop any newly discovered instance: a time window is not ownership proof.
    if (-not $Process.HasExited) {
        $Process.Kill()
        $null = $Process.WaitForExit(2000)
    }
    $failure = [InvalidOperationException]::new('FirstLaunchCleanupUnconfirmed')
    if ($inventoryFailure) { $failure.Data['SafeHResult'] = Get-MwbClientErrorHResult $inventoryFailure }
    throw $failure
}

function Invoke-MwbClientSetupWorker {
    param([Collections.IDictionary] $Request, [Collections.IDictionary] $Report, [string] $Directory)

    $launcher = $null
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
            Register-MwbClientManifest $Request.Package
        }
        else {
            $Report.Action = 'InboxFirstLaunch'
            $Report.Cleanup.Status = 'Pending'
            Save-MwbClientWorkerReport $Report $Directory
            $launcher = Start-MwbClientFirstLaunch $Directory ([guid]$Request.RunId)
        }
        $timer = [Diagnostics.Stopwatch]::StartNew()
        do {
            $Report.After = Get-MwbClientReadiness
            Save-MwbClientWorkerReport $Report $Directory
            if ($Report.After.Status -eq 'Ready') { break }
            Start-Sleep -Seconds 5
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
            try { $Report.Cleanup = Complete-MwbClientFirstLaunch $launcher $Directory ([guid]$Request.RunId) }
            catch {
                $Report.Cleanup = [ordered]@{
                    Status = 'Failed'
                    ErrorCode = if ($_.Exception.Message -ceq 'GuestMarkerMismatch') { 'GuestMarkerMismatch' } else { 'FirstLaunchCleanupUnconfirmed' }
                    ErrorHResult = Get-MwbClientErrorHResult $_
                }
                $Report.Status = 'Failed'
            }
            finally { $launcher.Dispose() }
        }
        elseif ($Report.Cleanup.Status -eq 'Pending') { $Report.Cleanup.Status = 'NotStarted' }
        if ($Report.Action -ne 'None') {
            $Report.After = Get-MwbClientReadiness
            if ($Report.Status -eq 'Ready' -and $Report.After.Status -ne 'Ready') {
                $Report.Status = 'Failed'
                $Report.ErrorCode = 'FinalReadinessFailed'
            }
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
    foreach ($name in @('Action', 'ErrorCode', 'ErrorHResult', 'Before', 'After', 'Cleanup')) {
        $value = $Source[$name]
        if ($name -in @('Before', 'After', 'Cleanup')) {
            $copy = [ordered]@{}
            if ($value -isnot [Collections.IDictionary]) { throw 'InvalidClientSetupReport' }
            foreach ($field in @('Status', 'PackageCount', 'Healthy', 'AliasExists', 'AliasIsReparsePoint',
                'VersionExitCode', 'VersionTimedOut', 'VersionOutputComplete', 'Version', 'ErrorCode',
                'ErrorHResult', 'GuestObserved', 'LauncherStopped')) {
                if (-not $value.Contains($field)) { continue }
                $item = $value[$field]
                if ($null -ne $item) {
                    if ($field -eq 'ErrorCode' -and $item -cnotin $errorCodes) { throw 'InvalidClientSetupReport' }
                    if ($rules.ContainsKey($field)) {
                        if ($item -isnot [string] -or $item -cnotmatch $rules[$field]) { throw 'InvalidClientSetupReport' }
                    }
                    elseif ($field -in @('PackageCount', 'VersionExitCode')) {
                        if ($item -isnot [int] -and $item -isnot [long]) { throw 'InvalidClientSetupReport' }
                    }
                    elseif ($item -isnot [bool]) { throw 'InvalidClientSetupReport' }
                }
                $copy[$field] = $item
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
