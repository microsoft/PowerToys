# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

Set-StrictMode -Version 2.0

# A PS7 controller can pass its incompatible module search path to Windows PowerShell.
# Keep this process-local and use only the native legacy module roots.
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $env:PSModulePath = [string]::Join([IO.Path]::PathSeparator, @(
        [IO.Path]::Combine([Environment]::GetFolderPath('Windows'), 'System32\WindowsPowerShell\v1.0\Modules'),
        [IO.Path]::Combine([Environment]::GetFolderPath('ProgramFiles'), 'WindowsPowerShell\Modules')
    ))
}

function Write-RunJson {
    param([string]$Path, $Value)
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 30))
    # Legacy Sandbox mapped files do not reliably allow rename/delete.
    $stream = [IO.File]::Open($Path, 'Create', 'Write', ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush() }
    finally { $stream.Dispose() }
    $published = [IO.File]::Open("$Path.ready", 'OpenOrCreate', 'Write',
        ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    $published.Dispose()
}

function Write-Trace {
    # Append-only diagnostic timeline, independent from the immutable-generation JSON
    # publication used for cross-process protocol files. Never read back by the protocol;
    # only for post-mortem evidence when startup does not complete in time.
    param([string]$Message)
    $line = '[{0}] {1}{2}' -f ([DateTime]::UtcNow.ToString('o')), $Message, [Environment]::NewLine
    $stream = [IO.File]::Open("$OutputRoot\start-endpoint-trace.log", 'Append', 'Write',
        ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($line)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush()
    }
    finally { $stream.Dispose() }
}

function Read-RunJson {
    param([string]$Path)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        try {
            $stream = [IO.File]::Open($Path, 'Open', 'Read', ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
            $reader = [IO.StreamReader]::new($stream)
            try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ([string]::IsNullOrWhiteSpace($text)) { throw [IO.IOException]::new('Incomplete JSON publication.') }
            return ($text | ConvertFrom-Json -ErrorAction Stop)
        }
        catch [IO.IOException] {
            if ($watch.Elapsed.TotalSeconds -ge 3) { throw }
            Start-Sleep -Milliseconds 50
        }
        catch [ArgumentException] {
            if ($watch.Elapsed.TotalSeconds -ge 3) { throw }
            Start-Sleep -Milliseconds 50
        }
    } while ($watch.Elapsed.TotalSeconds -lt 3)
    throw "JSON publication did not complete: $Path"
}

function Write-BootstrapStage {
    param([string]$Stage, $Details = $null)
    $script:bootstrapStageNumber++
    $process = Get-Process -Id $PID -ErrorAction Stop
    Write-RunJson (Join-Path $OutputRoot ('bootstrap-{0:D3}.json' -f $script:bootstrapStageNumber)) @{
        Stage = $Stage; RunId = $script:config.RunId; Role = $script:config.Role
        TimestampUtc = [DateTime]::UtcNow.ToString('o')
        ElapsedMilliseconds = $script:bootstrapWatch.ElapsedMilliseconds
        WorkerCpuSeconds = $process.CPU; WorkerWorkingSetBytes = $process.WorkingSet64
        LogicalProcessors = [Environment]::ProcessorCount; Details = $Details
    }
}

function Write-EndpointCommandStage {
    param(
        [string]$Stage,
        [string]$Operation = '',
        [int]$RequestSequence = 0,
        [int]$ChildProcessId = 0,
        [long]$ElapsedMilliseconds = 0
    )
    try {
        if ($script:config.Role -ne 'Guest') { return }
        $runId = [guid]::Empty
        if (-not [guid]::TryParse([string]$script:config.RunId, [ref]$runId)) { return }
        if ($Stage -cnotin @(
            'RequestReadStarting', 'RequestDispatching', 'RequestFailed',
            'ResponsePublishing', 'ResponsePublished',
            'UiPreparing', 'UiProcessStarting', 'UiProcessStarted', 'UiWaiting',
            'UiProcessExited', 'UiTimedOut', 'UiOutputRead', 'UiResultParsed'
        )) { $Stage = 'Unknown' }
        if ($Operation -cnotin @(
            'Start', 'Connect', 'VerifyPeerMapping', 'ClipboardSharing', 'Transport',
            'FocusInput', 'ClearInput', 'PublishClipboard', 'Observe', 'Evidence', 'StopPeers', 'Finish',
            'search', 'inspect', 'invoke', 'set-value', 'get-property', ''
        )) { $Operation = 'Unknown' }
        $script:commandStageNumber++
        # One bounded snapshot, never request/UI arguments, values, trees, or exceptions.
        Write-RunJson "$OutputRoot\command-stage.json" @{
            RunId = $runId.ToString(); TimestampUtc = [DateTime]::UtcNow.ToString('o')
            StageNumber = $script:commandStageNumber; Stage = $Stage; Operation = $Operation
            RequestSequence = $RequestSequence; WorkerProcessId = $PID; ChildProcessId = $ChildProcessId
            ElapsedMilliseconds = $ElapsedMilliseconds
        }
    }
    catch { }
}

function Expand-GuestRuntime {
    param([string]$Archive, [string]$Destination, [DateTime]$DeadlineUtc)
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = "$env:WINDIR\System32\tar.exe"
    $start.Arguments = '-xf "{0}" -C "{1}"' -f $Archive, $Destination
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $process = [Diagnostics.Process]::Start($start)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try {
        while (-not $process.WaitForExit(15000)) {
            Write-BootstrapStage 'ExtractionProgress' @{
                ProcessId = $process.Id; ElapsedMilliseconds = $watch.ElapsedMilliseconds
            }
            if ([DateTime]::UtcNow -ge $DeadlineUtc) {
                throw 'Guest runtime extraction exceeded the bootstrap deadline; see extraction progress.'
            }
        }
        if ($process.ExitCode -ne 0) { throw "Guest runtime extraction failed with exit code $($process.ExitCode)." }
        Write-BootstrapStage 'RuntimeExtracted' @{
            ElapsedMilliseconds = $watch.ElapsedMilliseconds
        }
    }
    finally {
        if (-not $process.HasExited) { $process.Kill(); $null = $process.WaitForExit(5000) }
        $process.Dispose()
    }
}

function Measure-RuntimeRead {
    param([string]$Path, [int]$BufferBytes)
    $buffer = [byte[]]::new($BufferBytes)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete), $BufferBytes, [IO.FileOptions]::SequentialScan)
    $bytesRead = 0L
    try {
        while ($bytesRead -lt 8MB) {
            $count = $stream.Read($buffer, 0, [Math]::Min($buffer.Length, 8MB - $bytesRead))
            if ($count -eq 0) { break }
            $bytesRead += $count
        }
    }
    finally { $stream.Dispose() }
    @{ BufferBytes = $BufferBytes; BytesRead = $bytesRead; ElapsedMilliseconds = $watch.ElapsedMilliseconds }
}

function Copy-GuestRuntimeArchive {
    param([string]$Source, [string]$Destination, [string]$ExpectedSha256)
    if ($ExpectedSha256 -notmatch '^[0-9A-Fa-f]{64}$') { throw 'Missing protected guest archive hash.' }
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $buffer = [byte[]]::new(4MB)
    $hash = [Security.Cryptography.SHA256]::Create()
    $sourceStream = $null
    $destinationStream = $null
    $bytesCopied = 0L
    try {
        $sourceStream = [IO.FileStream]::new($Source, [IO.FileMode]::Open, [IO.FileAccess]::Read,
            [IO.FileShare]::Read, $buffer.Length, [IO.FileOptions]::SequentialScan)
        $destinationStream = [IO.FileStream]::new($Destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write,
            [IO.FileShare]::None, $buffer.Length, [IO.FileOptions]::SequentialScan)
        while (($count = $sourceStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $destinationStream.Write($buffer, 0, $count)
            $null = $hash.TransformBlock($buffer, 0, $count, $null, 0)
            $bytesCopied += $count
        }
        $null = $hash.TransformFinalBlock([byte[]]@(), 0, 0)
        $actualHash = [BitConverter]::ToString($hash.Hash).Replace('-', '')
        if ($actualHash -ine $ExpectedSha256) { throw 'Locally copied guest archive hash differs from protected provisioning.' }
    }
    finally {
        if ($destinationStream) { $destinationStream.Dispose() }
        if ($sourceStream) { $sourceStream.Dispose() }
        $hash.Dispose()
    }
    @{ BytesCopied = $bytesCopied; ElapsedMilliseconds = $watch.ElapsedMilliseconds; Sha256 = $actualHash }
}

function Get-MachineName {
    $name = [Net.Dns]::GetHostName()
    $name.Substring(0, [Math]::Min(32, $name.Length)).Trim()
}

function Read-LatestEndpointLease {
    param([string]$Directory, [string]$RunId, [int]$AfterSequence)
    $latest = [IO.Directory]::EnumerateFiles($Directory, '*.json.ready') |
        Sort-Object -Descending | Select-Object -First 1
    if (-not $latest) { throw 'No committed endpoint lease was published.' }
    $name = [IO.Path]::GetFileName($latest)
    if ($name -notmatch '^([0-9]{8})\.json\.ready$') { throw 'Invalid lease generation filename.' }
    $number = [int]$Matches[1]
    if ($number -lt $AfterSequence) { throw 'Lease publication regressed.' }
    if ($number -eq $AfterSequence) { return }
    $candidate = Read-RunJson $latest.Substring(0, $latest.Length - '.ready'.Length)
    if ($candidate.RunId -ne $RunId -or $candidate.Sequence -ne $number) {
        throw 'Lease generation correlation mismatch.'
    }
    $candidate
}

function Confirm-EndpointLease {
    param([string]$Directory, [string]$RunId, $Lease)
    if (-not $Lease) { throw 'No initial host lease was published.' }
    if ($Lease.RunId -ne $RunId) { throw 'Lease generation correlation mismatch.' }
    if (([DateTime]::UtcNow - ([DateTime]$Lease.TimestampUtc).ToUniversalTime()).TotalSeconds -gt 45) {
        # A paused reader can resume with an obsolete selection while publication
        # continued. Re-observe once before declaring the host dead; never re-date a lease.
        $latest = Read-LatestEndpointLease $Directory $RunId ([int]$Lease.Sequence)
        if ($latest) { $Lease = $latest }
    }
    if (([DateTime]::UtcNow - ([DateTime]$Lease.TimestampUtc).ToUniversalTime()).TotalSeconds -gt 45) {
        throw "Host lease expired; observed $($Lease.TimestampUtc), now $([DateTime]::UtcNow.ToString('o')); stopping only this endpoint."
    }
    $Lease
}

function Get-EndpointNetwork {
    $addresses = @()
    $gateways = @()
    foreach ($adapter in [Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()) {
        if (-not $adapter.Supports([Net.NetworkInformation.NetworkInterfaceComponent]::IPv4)) { continue }
        $properties = $adapter.GetIPProperties()
        $index = $properties.GetIPv4Properties().Index
        foreach ($entry in $properties.UnicastAddresses) {
            if ($entry.Address.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) { continue }
            $prefix = 0
            foreach ($octet in $entry.IPv4Mask.GetAddressBytes()) {
                for ($bit = 7; $bit -ge 0; $bit--) { $prefix += ($octet -shr $bit) -band 1 }
            }
            $addresses += [pscustomobject]@{
                IPAddress = $entry.Address.ToString(); PrefixLength = $prefix
                InterfaceIndex = $index; InterfaceAlias = $adapter.Name
            }
        }
        foreach ($entry in $properties.GatewayAddresses) {
            if ($entry.Address.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork -and
                -not $entry.Address.Equals([Net.IPAddress]::Any)) {
                $gateways += [pscustomobject]@{ InterfaceIndex = $index; NextHop = $entry.Address.ToString() }
            }
        }
    }
    @{ Addresses = $addresses; Gateways = $gateways }
}

function Assert-CleanEndpoint {
    if (@(Get-Process | Where-Object { $_.ProcessName -match '^PowerToys($|\.)' }).Count) {
        throw 'Refusing a pre-existing PowerToys endpoint.'
    }
}

function Write-EndpointJournal {
    Write-RunJson "$OutputRoot\endpoint-journal.json" ([ordered]@{
        FormatVersion = 1; RunId = $script:config.RunId; Role = $script:config.Role
        ProductRoot = $ProductRoot; SettingsRoot = $script:settingsRoot
        Worker = [Microsoft.MouseWithoutBorders.UITests.ProcessIdentity]::Capture($PID)
        Processes = @($script:owned.ToArray()); Settings = @($script:backups)
        Status = $script:status; GuestFirewallRule = $script:firewallRule
        GuestFirewallOwnership = $script:firewallOwnership
        SettingsRestored = $script:settingsRestored; ClipboardRestored = $script:clipboardRestored
        ClipboardMayHaveChanged = $script:clipboardMayHaveChanged
        TimestampUtc = [DateTime]::UtcNow.ToString('o')
    })
}

function Update-OwnedProcesses {
    $candidates = @(Get-Process | Where-Object { $_.ProcessName -match '^PowerToys($|\.)' })
    do {
        $added = $false
        foreach ($process in $candidates) {
            if (@($script:owned | Where-Object { $_.Id -eq $process.Id }).Count) { continue }
            $record = $null
            $lastError = $null
            $stillAlive = $false
            # QueryFullProcessImageName/toolhelp32 (inside ProcessIdentity.Capture) can
            # transiently fail with ERROR_GEN_FAILURE ("A device attached to the system is
            # not functioning") while a child process such as a WinUI flyout helper is mid-
            # exit: its EPROCESS is still enumerable for a short window after its image
            # section is torn down. This is worse under nested virtualization, where that
            # window is longer. Retry briefly instead of aborting the whole endpoint for
            # what is normally a process finishing its own exit.
            for ($attempt = 1; $attempt -le 10; $attempt++) {
                try { $record = [Microsoft.MouseWithoutBorders.UITests.ProcessIdentity]::Capture($process.Id) }
                catch { $lastError = $_; $record = $null }
                if ($record) { break }
                $current = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
                if (-not $current) { $stillAlive = $false; break }
                $current.Refresh()
                $stillAlive = -not $current.HasExited
                if (-not $stillAlive) { break }
                if ($attempt -lt 10) { Start-Sleep -Milliseconds 150 }
            }
            if (-not $record) {
                if ($stillAlive) {
                    # Still alive but unreadable after retries: skip this candidate for this
                    # cycle rather than throw and abort the whole endpoint. Stop-Endpoint's
                    # remaining-process wait and Assert-CleanEndpoint still refuse to finish
                    # while it (or a reused PID) is present, so cleanup correctness holds,
                    # and the next Update-OwnedProcesses call retries this PID from scratch.
                    Write-RunJson "$OutputRoot\process-identity-transient-failure.json" @{
                        ProcessId = $process.Id; Name = $process.ProcessName
                        Error = $lastError.Exception.Message; TimestampUtc = [DateTime]::UtcNow.ToString('o')
                    }
                    Write-Trace ("Capture unreadable after 10 retries: pid={0} name={1} error={2}" -f `
                        $process.Id, $process.ProcessName, $lastError.Exception.Message)
                }
                continue
            }
            if (-not $record.Path.StartsWith($ProductRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { continue }
            # Do not require the parent to still be alive: WinUI3/WindowsAppRuntime
            # unpackaged apps (observed for both PowerToys.Settings.exe and
            # PowerToys.QuickAccess.exe here) self-relaunch a child copy of themselves and
            # then exit, so the real, long-lived, windowed process is a grandchild of
            # Runner whose immediate parent has already exited by the time it is captured.
            # Id + SessionId + start-time ordering already guard against a reused PID being
            # mistaken for this lineage.
            $parent = @($script:owned | Where-Object { $_.Id -eq $record.ParentId })
            if ($parent.Count -ne 1 -or $record.SessionId -ne $parent[0].SessionId -or
                $record.StartTimeUtc -lt $parent[0].StartTimeUtc) {
                Write-Trace ("Captured but not yet ownable: pid={0} path={1} parentId={2} ownedParentMatches={3}" -f `
                    $record.Id, $record.Path, $record.ParentId, $parent.Count)
                continue
            }
            $script:owned.Add($record)
            $added = $true
        }
    } while ($added)
    Write-EndpointJournal
}

function Save-Settings {
    # Never place profile backups in mapped folders or published test results.
    if (Test-Path -LiteralPath $script:backupRoot) { throw 'A private recovery directory already exists for this RunId.' }
    $null = New-Item -ItemType Directory -Path $script:backupRoot
    foreach ($relative in @('settings.json', 'oobe_settings.json', 'MouseWithoutBorders\settings.json')) {
        $path = Join-Path $script:settingsRoot $relative
        $exists = Test-Path -LiteralPath $path -PathType Leaf
        $backup = Join-Path $script:backupRoot ($script:backups.Count.ToString() + '.bin')
        $hash = ''
        if ($exists) {
            [IO.File]::WriteAllBytes($backup, [IO.File]::ReadAllBytes($path))
            $hash = (Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
        }
        $script:backups += [ordered]@{ Path = $path; Existed = $exists; Backup = $backup; Sha256 = $hash }
    }
    Write-EndpointJournal
}

function Restore-Settings {
    if ($script:settingsRestored) { return }
    foreach ($item in $script:backups) {
        if ($item.Existed -and (Get-FileHash -LiteralPath $item.Backup -Algorithm SHA256).Hash -ne $item.Sha256) {
            throw 'Settings backup hash mismatch; refusing restoration.'
        }
    }
    foreach ($item in $script:backups) {
        if ($item.Existed) {
            [IO.File]::WriteAllBytes($item.Path, [IO.File]::ReadAllBytes($item.Backup))
            if ((Get-FileHash -LiteralPath $item.Path -Algorithm SHA256).Hash -ne $item.Sha256) {
                throw 'Settings restoration did not preserve exact bytes.'
            }
        }
        elseif (Test-Path -LiteralPath $item.Path) { Remove-Item -LiteralPath $item.Path -Force }
    }
    $script:settingsRestored = $true
}

function Test-SettingsWindowReady {
    param([bool]$Responding, [object[]]$Windows)
    # This pilot uses English Settings. WinUI's initial "WinUI Desktop" HWND
    # exists before the XAML content and must not trigger UI Automation.
    $settingsWindows = @($Windows | Where-Object {
        $_.Visible -and -not $_.HasOwner -and
        $_.Title -in 'PowerToys Settings', 'Administrator: PowerToys Settings'
    })
    return $Responding -and $settingsWindows.Count -eq 1
}

function Get-ReadySettingsCandidates {
    param([object[]]$Candidates)
    $Candidates | Where-Object { Test-SettingsWindowReady $_.Responding $_.Windows }
}

function New-EndpointFirewallPolicy {
    New-Object -ComObject HNetCfg.FwPolicy2
}

function New-EndpointFirewallRuleObject {
    New-Object -ComObject HNetCfg.FWRule
}

function Release-EndpointFirewallComObject {
    param($Value)
    if ($null -ne $Value -and [Runtime.InteropServices.Marshal]::IsComObject($Value)) {
        $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($Value)
    }
}

function Read-EndpointFirewallRules {
    param($Policy, [string]$Name)
    $rules = $null
    $enumerator = $null
    try {
        $rules = $Policy.Rules
        $enumerator = [Management.Automation.LanguagePrimitives]::GetEnumerator($rules)
        if ($null -eq $enumerator) { throw 'Endpoint firewall rules could not be enumerated.' }
        while ($enumerator.MoveNext()) {
            $rule = $enumerator.Current
            try {
                if (-not [string]::Equals($rule.Name, $Name, [StringComparison]::OrdinalIgnoreCase)) { continue }
                [pscustomobject]@{
                    Name = [string]$rule.Name; Program = [string]$rule.ApplicationName
                    Protocol = [int]$rule.Protocol; LocalPorts = [string]$rule.LocalPorts
                    RemotePorts = [string]$rule.RemotePorts; LocalAddresses = [string]$rule.LocalAddresses
                    PeerAddress = [string]$rule.RemoteAddresses; Direction = [int]$rule.Direction
                    Action = [int]$rule.Action; Enabled = [bool]$rule.Enabled; Profiles = [int]$rule.Profiles
                    EdgeTraversal = [bool]$rule.EdgeTraversal; InterfaceTypes = [string]$rule.InterfaceTypes
                    ServiceName = [string]$rule.ServiceName
                }
            }
            finally { Release-EndpointFirewallComObject $rule }
        }
    }
    finally {
        if ($enumerator -is [IDisposable]) { $enumerator.Dispose() }
        Release-EndpointFirewallComObject $enumerator
        Release-EndpointFirewallComObject $rules
    }
}

function Test-EndpointFirewallRuleScope {
    param($Rule, $Ownership)
    $peerParts = @($Rule.PeerAddress.Trim().Split('/'))
    $peer = $null
    $exactPeer = $peerParts.Count -in 1, 2 -and
        [Net.IPAddress]::TryParse($peerParts[0], [ref]$peer) -and
        $peer.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork -and
        $peer.Equals([Net.IPAddress]::Parse($Ownership.PeerAddress)) -and
        ($peerParts.Count -eq 1 -or $peerParts[1] -in '32', '255.255.255.255')
    $ports = $Rule.LocalPorts -replace '\s', ''
    [string]::Equals($Rule.Name, $Ownership.Name, [StringComparison]::OrdinalIgnoreCase) -and
        [string]::Equals($Rule.Program, $Ownership.Program, [StringComparison]::OrdinalIgnoreCase) -and
        $Rule.Protocol -eq 6 -and $ports -match '^(15100,15101|15101,15100|15100-15101)$' -and
        $exactPeer -and $Rule.LocalAddresses -eq '*' -and $Rule.RemotePorts -eq '*' -and
        $Rule.Direction -eq 1 -and $Rule.Action -eq 1 -and $Rule.Enabled -and
        $Rule.Profiles -eq 2147483647 -and -not $Rule.EdgeTraversal -and
        $Rule.InterfaceTypes -eq 'All' -and [string]::IsNullOrEmpty($Rule.ServiceName)
}

function Save-EndpointFirewallOwnership {
    param($Ownership)
    $script:firewallOwnership = $Ownership
    $script:firewallRule = if ($Ownership.State -eq 'Verified') { $Ownership.Name } else { '' }
    Write-EndpointJournal
}

function Add-EndpointFirewallRule {
    param([string]$Name, [string]$Program, [string]$PeerAddress)
    $peer = [Net.IPAddress]::Parse($PeerAddress)
    if ($peer.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) {
        throw 'The endpoint firewall requires one IPv4 peer.'
    }
    $ownership = [pscustomobject]@{
        Name = $Name; Program = $Program; PeerAddress = $peer.ToString()
        State = 'Unclaimed'; Error = $null
        CreationReadback = @(); RemovalReadback = $null; AbsentVerifiedUtc = $null
    }
    $policy = $null
    $rules = $null
    $rule = $null
    try {
        $policy = New-EndpointFirewallPolicy
        if (@(Read-EndpointFirewallRules $policy $Name).Count) {
            throw 'Refusing a pre-existing endpoint firewall friendly name.'
        }
        $rule = New-EndpointFirewallRuleObject
        $rule.Name = $Name
        $rule.ApplicationName = $Program
        $rule.Protocol = 6
        $rule.LocalPorts = '15100,15101'
        $rule.RemotePorts = '*'
        $rule.LocalAddresses = '*'
        $rule.RemoteAddresses = $ownership.PeerAddress
        $rule.Direction = 1
        $rule.Action = 1
        $rule.Enabled = $true
        $rule.Profiles = 2147483647
        $rule.EdgeTraversal = $false
        $rule.InterfaceTypes = 'All'
        $rules = $policy.Rules
        # COM uses the friendly name, not NetSecurity's internal instance ID.
        # Do not claim or overwrite any same-name rule found before Add.
        if (@(Read-EndpointFirewallRules $policy $Name).Count) {
            throw 'An endpoint firewall friendly-name collision appeared before creation.'
        }
        $ownership.State = 'CreationPending'
        Save-EndpointFirewallOwnership $ownership
        $addError = $null
        try { $null = $rules.Add($rule) }
        catch { $addError = $_ }
        try {
            $observed = @(Read-EndpointFirewallRules $policy $Name)
            $ownership.CreationReadback = $observed
            if ($observed.Count -eq 1 -and (Test-EndpointFirewallRuleScope $observed[0] $ownership)) {
                $ownership.State = 'Verified'
            }
            elseif ($addError -and $observed.Count -eq 0) {
                $ownership.State = 'Absent'
                $ownership.AbsentVerifiedUtc = [DateTime]::UtcNow.ToString('o')
            }
            else {
                throw 'Endpoint firewall creation did not produce one exact scoped rule.'
            }
        }
        catch {
            $ownership.State = 'Uncertain'
            $ownership.Error = $_.Exception.Message
            Save-EndpointFirewallOwnership $ownership
            throw
        }
        if ($addError) { $ownership.Error = $addError.Exception.Message }
        Save-EndpointFirewallOwnership $ownership
        if ($addError) { throw $addError }
    }
    finally {
        Release-EndpointFirewallComObject $rule
        Release-EndpointFirewallComObject $rules
        Release-EndpointFirewallComObject $policy
    }
}

function Remove-EndpointFirewallRule {
    param($Ownership)
    if ($null -eq $Ownership -or $Ownership.State -in 'Absent', 'Removed') { return }
    if ($Ownership.State -ne 'Verified') {
        throw 'Endpoint firewall ownership is uncertain; refusing removal.'
    }
    $policy = $null
    $rules = $null
    $scopeVerified = $false
    try {
        $policy = New-EndpointFirewallPolicy
        $observed = @(Read-EndpointFirewallRules $policy $Ownership.Name)
        $Ownership.RemovalReadback = $observed
        if ($observed.Count -gt 0) {
            if ($observed.Count -ne 1 -or -not (Test-EndpointFirewallRuleScope $observed[0] $Ownership)) {
                $Ownership.State = 'Uncertain'
                throw 'Endpoint firewall scope changed; refusing removal.'
            }
            $scopeVerified = $true
            $rules = $policy.Rules
            $Ownership.State = 'RemovalPending'
            Save-EndpointFirewallOwnership $Ownership
            $removeError = $null
            try { $null = $rules.Remove($Ownership.Name) }
            catch { $removeError = $_ }
            $remaining = @(Read-EndpointFirewallRules $policy $Ownership.Name)
            $Ownership.RemovalReadback = $remaining
            if ($remaining.Count) {
                if ($remaining.Count -ne 1 -or -not (Test-EndpointFirewallRuleScope $remaining[0] $Ownership)) {
                    $Ownership.State = 'Uncertain'
                }
                else { $Ownership.State = 'Verified' }
                throw 'Endpoint firewall rule remains after removal.'
            }
            $Ownership.State = 'Removed'
            $Ownership.AbsentVerifiedUtc = [DateTime]::UtcNow.ToString('o')
            if ($removeError) { throw $removeError }
        }
        else {
            $Ownership.State = 'Removed'
            $Ownership.AbsentVerifiedUtc = [DateTime]::UtcNow.ToString('o')
        }
        Save-EndpointFirewallOwnership $Ownership
    }
    catch {
        if ($Ownership.State -eq 'RemovalPending' -or (-not $scopeVerified -and $Ownership.State -ne 'Removed')) {
            $Ownership.State = 'Uncertain'
        }
        $Ownership.Error = $_.Exception.Message
        Save-EndpointFirewallOwnership $Ownership
        throw
    }
    finally {
        Release-EndpointFirewallComObject $rules
        Release-EndpointFirewallComObject $policy
    }
}

function Start-Endpoint {
    param([string]$PeerName, [string]$PeerAddress, $GlobalSettings, [string]$ExpectedLocalAddress)
    Assert-CleanEndpoint
    if ($script:owned.Count -or $script:backups.Count) { throw 'Endpoint already started.' }
    if ($PeerName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{0,31}$' -or
        ([Net.IPAddress]::Parse($PeerAddress)).AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) {
        throw 'Invalid peer identity.'
    }
    $network = Get-EndpointNetwork
    $local = @($network.Addresses |
        Where-Object { $_.IPAddress -eq $ExpectedLocalAddress })
    if ($local.Count -ne 1) {
        throw "NETWORK_CHANGED: expected local address $ExpectedLocalAddress is no longer present. Reprovision the actual Sandbox network before retrying."
    }
    if ($script:config.Role -eq 'Guest') {
        $gateways = @($network.Gateways |
            Where-Object { $_.InterfaceIndex -eq $local[0].InterfaceIndex } |
            Select-Object -ExpandProperty NextHop -Unique)
        if ($gateways.Count -ne 1 -or $gateways[0] -ne $PeerAddress) {
            throw "NETWORK_CHANGED: guest gateway [$($gateways -join ', ')] does not match provisioned host $PeerAddress. No guest firewall rule or product was started."
        }
    }
    Save-Settings
    $logRoot = Join-Path $script:settingsRoot 'MouseWithoutBorders\Logs'
    if (Test-Path -LiteralPath $logRoot) {
        foreach ($file in Get-ChildItem -LiteralPath $logRoot -File -Recurse) {
            $script:logOffsets[$file.FullName] = $file.Length
        }
    }
    $null = New-Item -ItemType Directory -Path "$script:settingsRoot\MouseWithoutBorders" -Force
    Write-RunJson "$script:settingsRoot\settings.json" $GlobalSettings
    Write-RunJson "$script:settingsRoot\oobe_settings.json" @{ openedAtFirstLaunch = $true }
    $settings = Read-RunJson "$PSScriptRoot\MwbSettings.template.json"
    $script:expectedPeerMapping = "$PeerName $PeerAddress"
    $settings.properties.Name2IP.value = $script:expectedPeerMapping
    Write-RunJson "$script:settingsRoot\MouseWithoutBorders\settings.json" $settings
    $null = Assert-PeerMapping 'seeded'
    if ($script:config.Role -eq 'Guest') {
        Add-EndpointFirewallRule -Name "PowerToys.Mwb.Guest.$($script:config.RunId)" `
            -Program (Join-Path $ProductRoot 'PowerToys.MouseWithoutBorders.exe') -PeerAddress $PeerAddress
    }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = Join-Path $ProductRoot 'PowerToys.exe'
    $start.WorkingDirectory = $ProductRoot
    $start.Arguments = '--open-settings'
    $start.UseShellExecute = $false
    $start.EnvironmentVariables['POWERTOYS_MWB_ALLOW_NONCONSOLE'] = '1'
    $process = [Diagnostics.Process]::Start($start)
    try { $script:owned.Add([Microsoft.MouseWithoutBorders.UITests.ProcessIdentity]::Capture($process.Id)) }
    catch {
        if (-not $process.HasExited) { $process.Kill(); $null = $process.WaitForExit(15000) }
        throw
    }
    finally { $process.Dispose() }
    $script:status = 'Running'
    Write-EndpointJournal
    $startupBudgetMinutes = if ($script:config.Role -eq 'Guest') { 8 } else { 5 }
    $deadline = [DateTime]::UtcNow.AddMinutes($startupBudgetMinutes)
    $lastTraceUtc = [DateTime]::MinValue
    $readySettingsPid = 0
    $readySamples = 0
    do {
        Update-OwnedProcesses
        $settingsProcess = @($script:owned | Where-Object { [IO.Path]::GetFileName($_.Path) -eq 'PowerToys.Settings.exe' -and $_.IsCurrent() })
        $mwb = @($script:owned | Where-Object { [IO.Path]::GetFileName($_.Path) -eq 'PowerToys.MouseWithoutBorders.exe' -and $_.IsCurrent() })
        $candidateStates = @()
        foreach ($candidate in $settingsProcess) {
            try {
                $settingsInfo = Get-Process -Id $candidate.Id -ErrorAction Stop
                $windows = @([Microsoft.MouseWithoutBorders.UITests.NativeSupport]::WindowsForProcess($candidate.Id))
                $responding = $settingsInfo.Responding
                $candidateStates += @{ ProcessId = $candidate.Id; Responding = $responding; Windows = $windows }
            }
            catch [Microsoft.PowerShell.Commands.ProcessCommandException] {
                Write-Trace ("Settings vanished during readiness observation: pid={0}" -f $candidate.Id)
            }
        }
        # A WinUI launcher can remain alive alongside its windowed descendant.
        # Bind UI Automation to the one initialized window owner, not a process count.
        $readyCandidates = @(Get-ReadySettingsCandidates $candidateStates)
        $settingsReady = $readyCandidates.Count -eq 1
        if ($settingsReady -and $mwb.Count -eq 1) {
            if ($readySettingsPid -eq $readyCandidates[0].ProcessId) { $readySamples++ }
            else { $readySettingsPid = $readyCandidates[0].ProcessId; $readySamples = 1 }
        }
        else { $readySettingsPid = 0; $readySamples = 0 }
        if ($readySamples -ge 2) {
            # Confirm Settings is still alive right before committing to success: it has been
            # observed to exit again (another self-relaunch generation, or a genuine crash)
            # in the narrow window between the checks above and here.
            $stillAlive = $false
            try {
                $confirm = Get-Process -Id $readySettingsPid -ErrorAction Stop
                $stillAlive = -not $confirm.HasExited
            }
            catch [Microsoft.PowerShell.Commands.ProcessCommandException] { $stillAlive = $false }
            if ($stillAlive) {
                $script:settingsPid = $readySettingsPid
                if ($script:config.Role -eq 'Guest') {
                    try { Save-SettingsDiagnostics 'startup' }
                    catch {
                        # A best-effort UI-tree snapshot must never turn an already-confirmed
                        # successful start into a reported failure.
                        Write-Trace ("Save-SettingsDiagnostics failed after startup succeeded: {0}" -f $_.Exception.Message)
                    }
                }
                return @{ SettingsProcessId = $script:settingsPid; MwbProcessId = $mwb[0].Id }
            }
            Write-Trace ("Settings vanished just before returning success: pid={0}" -f $readySettingsPid)
        }
        if (([DateTime]::UtcNow - $lastTraceUtc).TotalSeconds -ge 2) {
            $lastTraceUtc = [DateTime]::UtcNow
            $liveCandidates = @(Get-Process | Where-Object { $_.ProcessName -match '^PowerToys($|\.)' } |
                ForEach-Object { '{0}:{1}' -f $_.Id, $_.ProcessName })
            $ownedSummary = @($script:owned | ForEach-Object { '{0}:{1}' -f $_.Id, [IO.Path]::GetFileName($_.Path) })
            Write-Trace ("Poll: live=[{0}] owned=[{1}] settingsOwned={2} mwbOwned={3} settingsReady={4} readySamples={5} readyCandidates={6}" -f `
                ($liveCandidates -join ', '), ($ownedSummary -join ', '), $settingsProcess.Count, $mwb.Count, $settingsReady, $readySamples, $readyCandidates.Count)
            Write-RunJson "$OutputRoot\settings-readiness.json" @{ Candidates = $candidateStates; TimestampUtc = [DateTime]::UtcNow.ToString('o') }
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    # Diagnose exactly what state Settings/MWB were in: a WinUI3 process with no top-level
    # window at all is a very different problem than one that simply never launched.
    $diagnostic = [ordered]@{
        Role = $script:config.Role; TimestampUtc = [DateTime]::UtcNow.ToString('o')
        SettingsOwnedCount = $settingsProcess.Count; MwbOwnedCount = $mwb.Count
    }
    if ($settingsProcess.Count -eq 1) {
        $settingsInfo = Get-Process -Id $settingsProcess[0].Id -ErrorAction SilentlyContinue
        if ($settingsInfo) {
            $settingsInfo.Refresh()
            $diagnostic.SettingsResponding = $settingsInfo.Responding
            $diagnostic.SettingsMainWindowTitle = $settingsInfo.MainWindowTitle
            $diagnostic.SettingsThreadCount = $settingsInfo.Threads.Count
            $diagnostic.SettingsWindows = @([Microsoft.MouseWithoutBorders.UITests.NativeSupport]::WindowsForProcess($settingsProcess[0].Id))
        }
    }
    Write-RunJson "$OutputRoot\startup-timeout-diagnostics.json" $diagnostic
    throw "Owned MWB and responsive, initialized Settings did not both start within $startupBudgetMinutes minutes."
}

function Stop-Endpoint {
    Update-OwnedProcesses
    # The first record is Runner: stop it before children so it cannot restart MWB.
    foreach ($record in $script:owned) { $record.Stop() }
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $remaining = @(Get-Process | Where-Object { $_.ProcessName -match '^PowerToys($|\.)' })
        if (-not $remaining.Count) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    Assert-CleanEndpoint
    Restore-Settings
    Remove-EndpointFirewallRule $script:firewallOwnership
    $script:status = 'PeersStoppedSettingsRestored'
    Write-EndpointJournal
}

function Invoke-GuestUi {
    param([string[]]$Arguments)
    $launchWatch = [Diagnostics.Stopwatch]::StartNew()
    Write-EndpointCommandStage 'UiPreparing' $Arguments[0] $script:requestNumber
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $WinApp
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.EnvironmentVariables['WINAPP_CLI_TELEMETRY_OPTOUT'] = '1'
    $start.EnvironmentVariables['WINAPP_CLI_UPDATE_CHECK'] = '0'
    $allArgs = @('ui') + $Arguments + @('-a', $script:settingsPid.ToString(), '--json')
    $start.Arguments = (@($allArgs | ForEach-Object {
        '"' + [regex]::Replace([regex]::Replace($_, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
    })) -join ' '
    Write-EndpointCommandStage 'UiProcessStarting' $Arguments[0] $script:requestNumber
    $process = [Diagnostics.Process]::Start($start)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try {
        Write-EndpointCommandStage 'UiProcessStarted' $Arguments[0] $script:requestNumber $process.Id $launchWatch.ElapsedMilliseconds
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        Write-EndpointCommandStage 'UiWaiting' $Arguments[0] $script:requestNumber $process.Id $watch.ElapsedMilliseconds
        if (-not $process.WaitForExit(90000)) {
            $process.Kill()
            $null = $process.WaitForExit(5000)
            Write-EndpointCommandStage 'UiTimedOut' $Arguments[0] $script:requestNumber $process.Id $watch.ElapsedMilliseconds
            throw "Guest winapp $($Arguments[0]) exceeded 90 seconds."
        }
        Write-EndpointCommandStage 'UiProcessExited' $Arguments[0] $script:requestNumber $process.Id $watch.ElapsedMilliseconds
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr), 5000)) {
            throw 'Guest winapp output handles did not close within five seconds of process exit.'
        }
        $raw = $stdout.GetAwaiter().GetResult()
        $null = $stderr.GetAwaiter().GetResult()
        Write-EndpointCommandStage 'UiOutputRead' $Arguments[0] $script:requestNumber $process.Id $watch.ElapsedMilliseconds
        # Never put command arguments, UI trees, or stderr in errors: Connect contains a key.
        try { $data = $raw | ConvertFrom-Json -ErrorAction Stop }
        catch { throw "Guest winapp $($Arguments[0]) did not return valid JSON." }
        if ($process.ExitCode -ne 0 -and -not ($Arguments[0] -eq 'search' -and $process.ExitCode -eq 1 -and $data.matchCount -eq 0)) {
            throw "Guest winapp $($Arguments[0]) failed (exit $($process.ExitCode))."
        }
        Write-EndpointCommandStage 'UiResultParsed' $Arguments[0] $script:requestNumber $process.Id $watch.ElapsedMilliseconds
        return $data
    }
    finally {
        Write-RunJson "$OutputRoot\last-ui-command.json" @{
            Verb = $Arguments[0]; ElapsedMilliseconds = $watch.ElapsedMilliseconds
            Exited = $process.HasExited; TimestampUtc = [DateTime]::UtcNow.ToString('o')
        }
        $process.Dispose()
    }
}

function Find-GuestButton {
    param([string]$Name)
    $result = Invoke-GuestUi @('search', $Name)
    $matches = @($result.matches | Where-Object {
        $_.type -eq 'Button' -and $_.name -ceq $Name -and $_.isEnabled -and -not $_.isOffscreen
    })
    if ($matches.Count -ne 1) { throw "Expected one enabled guest '$Name' button, found $($matches.Count)." }
    $matches[0].selector
}

function Connect-Guest {
    param([string]$Key, [string]$PeerName)
    $deadline = [DateTime]::UtcNow.AddMinutes(3)
    do {
        $nav = Invoke-GuestUi @('search', 'MouseWithoutBordersNavItem')
        $group = if ($nav.matchCount -eq 0) { Invoke-GuestUi @('search', 'InputOutputNavItem') } else { @{ matchCount = 0 } }
        if ($nav.matchCount -gt 0 -or $group.matchCount -gt 0) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($nav.matchCount -eq 0 -and $group.matchCount -eq 0) {
        Save-SettingsDiagnostics 'navigation-failed'
        throw 'Guest Settings navigation did not become ready.'
    }
    if ($nav.matchCount -eq 0) { $null = Invoke-GuestUi @('invoke', 'InputOutputNavItem') }
    $null = Invoke-GuestUi @('invoke', 'MouseWithoutBordersNavItem')
    $fieldWait = [Diagnostics.Stopwatch]::StartNew()
    do {
        $field = Invoke-GuestUi @('search', 'ConnectPCNameTextBox')
        if ($field.matchCount -gt 0) { break }
        $button = Invoke-GuestUi @('search', 'Security key')
        $expand = @($button.matches | Where-Object {
            $_.type -eq 'Button' -and $_.name -eq 'Security key' -and
            $_.PSObject.Properties['expandState'] -and $_.expandState -eq 'collapsed'
        })
        if ($expand.Count -eq 1) {
            $null = Invoke-GuestUi @('invoke', $expand[0].selector)
            # A nested VM can spend most of the wait budget inside the action.
            # Always observe the resulting state, and never blindly collapse it.
            $field = Invoke-GuestUi @('search', 'ConnectPCNameTextBox')
            if ($field.matchCount -gt 0) { break }
        }
        Start-Sleep -Milliseconds 250
    } while ($fieldWait.Elapsed.TotalSeconds -lt 180)
    if ($field.matchCount -ne 1) { throw 'Guest Connect device field did not appear.' }
    $null = Invoke-GuestUi @('set-value', 'ConnectSecurityKeyTextBox', $Key)
    $null = Invoke-GuestUi @('set-value', 'ConnectPCNameTextBox', $PeerName)
    $controls = Invoke-GuestUi @('search', 'Connect')
    $field = @($controls.matches | Where-Object { $_.PSObject.Properties['automationId'] -and $_.automationId -eq 'ConnectPCNameTextBox' })
    $buttons = @($controls.matches | Where-Object {
        $_.type -eq 'Button' -and $_.name -eq 'Connect' -and $_.isEnabled -and -not $_.isOffscreen -and
        $field.Count -eq 1 -and [Math]::Abs($_.y - $field[0].y) -le 2
    })
    if ($buttons.Count -ne 1) { throw 'Guest input-row Connect button was ambiguous.' }
    $mapping = Assert-PeerMapping 'before-connect'
    $null = Invoke-GuestUi @('invoke', $buttons[0].selector)
    @{ Submitted = $true; Mapping = $mapping }
}

function Assert-PeerMapping {
    param([string]$Stage)
    if ([string]::IsNullOrEmpty($script:expectedPeerMapping)) { throw 'The endpoint has not seeded a peer mapping.' }
    $settings = Read-RunJson "$script:settingsRoot\MouseWithoutBorders\settings.json"
    $actual = [string]$settings.properties.Name2IP.value
    $evidence = @{
        Expected = $script:expectedPeerMapping; Actual = $actual
        MatchesExpected = $actual -ceq $script:expectedPeerMapping
        TimestampUtc = [DateTime]::UtcNow.ToString('o')
    }
    Write-RunJson "$OutputRoot\peer-mapping-$Stage.json" $evidence
    if (-not $evidence.MatchesExpected) {
        throw "Peer mapping changed at $Stage; refusing to connect to an unverified network destination."
    }
    $evidence
}

function Set-GuestClipboardSharing {
    param([bool]$Enabled)
    $card = Invoke-GuestUi @('search', 'MouseWithoutBordersShareClipboard')
    if ($card.matchCount -ne 1) { throw 'Guest clipboard settings card was ambiguous.' }
    $tree = Invoke-GuestUi @('inspect', $card.matches[0].selector, '-d', '4')
    function Find-Toggle($Node) {
        if ($Node -is [array]) { foreach ($child in $Node) { Find-Toggle $child }; return }
        if (-not $Node) { return }
        if ($Node.PSObject.Properties['className'] -and $Node.className -eq 'ToggleSwitch') { $Node }
        if ($Node.PSObject.Properties['children']) { Find-Toggle $Node.children }
        if ($Node.PSObject.Properties['elements']) { Find-Toggle $Node.elements }
        if ($Node.PSObject.Properties['windows']) { Find-Toggle $Node.windows }
    }
    $toggles = @(Find-Toggle $tree)
    if ($toggles.Count -ne 1) { throw 'Guest clipboard toggle was ambiguous.' }
    $settings = Read-RunJson "$script:settingsRoot\MouseWithoutBorders\settings.json"
    if ([bool]$settings.properties.ShareClipboard.value -ne $Enabled) {
        $null = Invoke-GuestUi @('invoke', $toggles[0].selector)
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $settings = Read-RunJson "$script:settingsRoot\MouseWithoutBorders\settings.json"
        if ([bool]$settings.properties.ShareClipboard.value -eq $Enabled) { return @{ Enabled = $Enabled } }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Guest clipboard UI did not persist its requested state.'
}

function Get-Transport {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    Write-RunJson "$OutputRoot\transport-probe.json" @{ Stage = 'OwnedProcesses'; TimestampUtc = [DateTime]::UtcNow.ToString('o') }
    Update-OwnedProcesses
    $mwb = @($script:owned | Where-Object { [IO.Path]::GetFileName($_.Path) -eq 'PowerToys.MouseWithoutBorders.exe' -and $_.IsCurrent() })
    if ($mwb.Count -ne 1) { throw 'Transport probe requires one owned MWB process.' }
    Write-RunJson "$OutputRoot\transport-probe.json" @{
        Stage = 'TcpTable'; TimestampUtc = [DateTime]::UtcNow.ToString('o'); ElapsedMilliseconds = $watch.ElapsedMilliseconds
    }
    $sockets = @([Microsoft.MouseWithoutBorders.UITests.TcpSocketTable]::Read($mwb[0].Id) | Where-Object {
        ($_.LocalPort -in 15100,15101 -or $_.RemotePort -in 15100,15101)
    })
    $connections = @($sockets | Where-Object { $_.State -eq 'Established' })
    $settings = Read-RunJson "$script:settingsRoot\MouseWithoutBorders\settings.json"
    Write-RunJson "$OutputRoot\transport-probe.json" @{
        Stage = 'Completed'; TimestampUtc = [DateTime]::UtcNow.ToString('o'); ElapsedMilliseconds = $watch.ElapsedMilliseconds
    }
    @{
        Connections = $connections; Sockets = $sockets; MwbProcessId = $mwb[0].Id
        MachineMatrix = @($settings.properties.MachineMatrixString)
        SwitchKey = [int]$settings.properties.HotKeySwitchMachine.value
    }
}

function Save-SettingsDiagnostics {
    param([string]$Label)
    $process = Get-Process -Id $script:settingsPid -ErrorAction Stop
    Write-RunJson "$OutputRoot\$Label-process.json" @{
        ProcessId = $process.Id; MainWindowHandle = $process.MainWindowHandle.ToInt64()
        MainWindowTitle = $process.MainWindowTitle; Responding = $process.Responding
        CpuSeconds = $process.CPU; TimestampUtc = [DateTime]::UtcNow.ToString('o')
    }
    try {
        $tree = Invoke-GuestUi @('inspect', '-d', '5')
        $text = $tree | ConvertTo-Json -Depth 30
        $settings = Read-RunJson "$script:settingsRoot\MouseWithoutBorders\settings.json"
        $key = $settings.properties.SecurityKey.value
        if ($key) { $text = $text.Replace($key, '[REDACTED]') }
        $text = [regex]::Replace($text, '("value"\s*:\s*)"(?:[^"\\]|\\.)*"', '$1"[REDACTED]"')
        [IO.File]::WriteAllText("$OutputRoot\$Label-ui.json", $text, [Text.UTF8Encoding]::new($false))
    }
    catch {
        Write-RunJson "$OutputRoot\$Label-ui-failure.json" @{ Error = $_.Exception.Message }
    }
}

function Save-EndpointEvidence {
    $script:receiver.CaptureDesktop("$OutputRoot\desktop.png")
    if ($script:config.Role -eq 'Guest') {
        $query = [Diagnostics.Eventing.Reader.EventLogQuery]::new('Application',
            [Diagnostics.Eventing.Reader.PathType]::LogName,
            '*[System[(EventID=1000 or EventID=1001 or EventID=1026)]]')
        $query.ReverseDirection = $true
        $reader = [Diagnostics.Eventing.Reader.EventLogReader]::new($query)
        $events = @()
        try {
            for ($i = 0; $i -lt 20; $i++) {
                $event = $reader.ReadEvent()
                if (-not $event) { break }
                try {
                    if ($event.TimeCreated.ToUniversalTime() -lt $script:startedUtc) { break }
                    $xml = $event.ToXml()
                    if ($xml -match 'PowerToys\.(Settings|QuickAccess)' -and $xml -notmatch '(?i)clipboard|clip.?board') {
                        $settingsPath = "$script:settingsRoot\MouseWithoutBorders\settings.json"
                        if (Test-Path -LiteralPath $settingsPath) {
                            $key = (Read-RunJson $settingsPath).properties.SecurityKey.value
                            if ($key) { $xml = $xml.Replace($key, '[REDACTED]') }
                        }
                        $events += @{ EventId = $event.Id; TimestampUtc = $event.TimeCreated.ToUniversalTime().ToString('o'); Xml = $xml }
                    }
                }
                finally { $event.Dispose() }
            }
        }
        finally { $reader.Dispose() }
        Write-RunJson "$OutputRoot\application-errors.json" @{ Events = $events }
    }
    $capturedLogs = 0
    # Also capture Settings' own log folder: it is the process that has been observed to
    # exit and respawn under a PID that ProcessIdentity.Capture can never read afterward, and
    # its own log is the most direct way to learn why it exited.
    foreach ($relativeLogRoot in @('MouseWithoutBorders\Logs', 'Settings\Logs', 'RunnerLogs')) {
        $logRoot = Join-Path $script:settingsRoot $relativeLogRoot
        if (-not (Test-Path -LiteralPath $logRoot)) { continue }
        $null = New-Item -ItemType Directory -Path "$OutputRoot\logs" -Force
        foreach ($file in Get-ChildItem -LiteralPath $logRoot -File -Recurse) {
            # Only logs from this run; redact the generated key rather than copying profile data.
            # An open log's last-write timestamp can lag behind appended data.
            if ($file.LastWriteTimeUtc -lt $script:startedUtc -and
                -not $script:logOffsets.ContainsKey($file.FullName)) { continue }
            $offset = 0
            if ($script:logOffsets.ContainsKey($file.FullName) -and $file.Length -ge $script:logOffsets[$file.FullName]) {
                $offset = $script:logOffsets[$file.FullName]
            }

            $stream = [IO.File]::Open($file.FullName, 'Open', 'Read', ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
            $null = $stream.Seek($offset, [IO.SeekOrigin]::Begin)
            $reader = [IO.StreamReader]::new($stream)
            try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
            # Keep only this run's appended data and never export clipboard/key diagnostics.
            $content = (($content -split "`r?`n") | Where-Object { $_ -notmatch '(?i)key|clipboard|clip.?board' }) -join "`r`n"
            $settingsPath = "$script:settingsRoot\MouseWithoutBorders\settings.json"
            if (Test-Path -LiteralPath $settingsPath) {
                $key = (Read-RunJson $settingsPath).properties.SecurityKey.value
                if ($key) { $content = $content.Replace($key, '[REDACTED]') }
            }
            $prefix = $relativeLogRoot.Split('\')[0]
            $name = $script:config.Role + '_' + $prefix + '_' + $file.FullName.Substring($logRoot.Length + 1).Replace('\', '_')
            [IO.File]::WriteAllText((Join-Path "$OutputRoot\logs" $name), $content, [Text.UTF8Encoding]::new($false))
            $capturedLogs++
        }
    }
    @{ Captured = $true; ScreenshotKind = 'TestReceiverControlOnly'; LogsCaptured = $capturedLogs }
}
