# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Shared by the host supervisor and the Windows PowerShell 5.1 guest bootstrap.
Set-StrictMode -Version 2.0

function Write-ExperimentJson {
    param([string]$Path, $Value)
    $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes(($Value | ConvertTo-Json -Depth 20))
    # Sandbox's mapped-folder server can hold files without delete sharing. Do not rename them.
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Create, [IO.FileAccess]::Write,
        ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush()
    }
    finally { $stream.Dispose() }
}

function Read-ExperimentJson {
    param([string]$Path)
    $deadline = [DateTime]::UtcNow.AddSeconds(3)
    do {
        $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
            ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
        $reader = New-Object IO.StreamReader($stream)
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            try { return ($text | ConvertFrom-Json -ErrorAction Stop) }
            catch [ArgumentException] {
                if ([DateTime]::UtcNow -ge $deadline) { throw }
            }
        }
        Start-Sleep -Milliseconds 50
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "JSON publication did not complete: $Path"
}

function Get-ExperimentIdentity {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try {
        [ordered]@{
            ComputerName = [Net.Dns]::GetHostName()
            UserSid = $identity.User.Value
        }
    }
    finally { $identity.Dispose() }
}

function Get-ExperimentDesktop {
    if (-not ('MwbExperimentDesktop' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class MwbExperimentDesktop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Point { public int X; public int Y; }
    [DllImport("kernel32.dll")] public static extern uint WTSGetActiveConsoleSessionId();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError=true)] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", SetLastError=true)] public static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder name, int length, out int needed);
    [DllImport("wtsapi32.dll", SetLastError=true)]
    public static extern bool WTSQuerySessionInformation(IntPtr server, int session, int infoClass, out IntPtr buffer, out int bytes);
    [DllImport("wtsapi32.dll")] public static extern void WTSFreeMemory(IntPtr buffer);
}
'@
    }
    $session = (Get-Process -Id $PID).SessionId
    $buffer = [IntPtr]::Zero
    $bytes = 0
    if (-not [MwbExperimentDesktop]::WTSQuerySessionInformation([IntPtr]::Zero, $session, 8, [ref]$buffer, [ref]$bytes)) {
        throw 'Cannot query the interactive session.'
    }
    try { $state = [Runtime.InteropServices.Marshal]::ReadInt32($buffer) }
    finally { [MwbExperimentDesktop]::WTSFreeMemory($buffer) }
    $desktop = [MwbExperimentDesktop]::OpenInputDesktop(0, $false, 1)
    $name = New-Object Text.StringBuilder 256
    if ($desktop -ne [IntPtr]::Zero) {
        try {
            if (-not [MwbExperimentDesktop]::GetUserObjectInformation($desktop, 2, $name, 512, [ref]$bytes)) {
                throw "Cannot read input desktop: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
            }
        }
        finally { $null = [MwbExperimentDesktop]::CloseDesktop($desktop) }
    }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $point = New-Object 'MwbExperimentDesktop+Point'
    $inputAvailable = [MwbExperimentDesktop]::GetCursorPos([ref]$point)
    try {
        [pscustomobject]@{
            ComputerName = [Net.Dns]::GetHostName()
            SessionId = $session
            ConsoleSessionId = [MwbExperimentDesktop]::WTSGetActiveConsoleSessionId()
            WtsState = $state
            InputDesktop = $name.ToString()
            ForegroundHwnd = [MwbExperimentDesktop]::GetForegroundWindow().ToInt64()
            InputAvailable = $inputAvailable
            IsSystem = $identity.IsSystem
            Elevated = (New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole(
                [Security.Principal.WindowsBuiltInRole]::Administrator)
        }
    }
    finally { $identity.Dispose() }
}

function Assert-ExperimentDesktop {
    param([switch]$AllowNonConsole)
    $desktop = Get-ExperimentDesktop
    if ($desktop.IsSystem -or $desktop.SessionId -eq 0 -or $desktop.WtsState -ne 0 -or
        $desktop.InputDesktop -ine 'Default' -or -not $desktop.InputAvailable) {
        throw "An active, unlocked default input desktop is required: $($desktop | ConvertTo-Json -Compress)"
    }
    if (-not $AllowNonConsole -and $desktop.SessionId -ne $desktop.ConsoleSessionId) {
        throw 'The non-console Debug experiment requires explicit -AllowNonConsole.'
    }
    $desktop
}

function Wait-ExperimentDesktop {
    param([DateTime]$DeadlineUtc, [switch]$AllowNonConsole)
    do {
        $desktop = Get-ExperimentDesktop
        if (-not $desktop.IsSystem -and $desktop.SessionId -gt 0 -and $desktop.WtsState -eq 0 -and
            $desktop.InputDesktop -ieq 'Default' -and $desktop.InputAvailable -and
            ($AllowNonConsole -or $desktop.SessionId -eq $desktop.ConsoleSessionId)) {
            return $desktop
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $DeadlineUtc)
    throw "The initial guest input desktop did not become ready: $($desktop | ConvertTo-Json -Compress)"
}

function Assert-NoPowerToys {
    if (@(Get-Process | Where-Object { $_.ProcessName -match '^PowerToys($|\.)' }).Count) {
        throw 'PowerToys is already running. Close it yourself before starting; no existing session will be commandeered.'
    }
}

function ConvertTo-MwbMachineName {
    param([string]$DnsName)
    if ([string]::IsNullOrWhiteSpace($DnsName)) { throw 'A DNS host name is required.' }
    # Match Common.GetMachineName; Sandbox DNS names can exceed the MWB protocol's 32 characters.
    $DnsName.Substring(0, [Math]::Min(32, $DnsName.Length)).Trim()
}

function Assert-ExperimentPayload {
    param($Manifest, [string]$InputRoot, [string]$ProductRoot)
    foreach ($entry in $Manifest.Files) {
        $root = if ($entry.Kind -eq 'Product') { $ProductRoot } else { $InputRoot }
        $path = Join-Path $root $entry.RelativePath
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Sha256) {
            throw "Payload changed or is missing: $path. Finish the Debug build and prepare a new destination."
        }
    }
}

function Get-ExperimentProcessRecord {
    param([Diagnostics.Process]$Process)
    if (-not ('MwbExperimentProcess' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
public static class MwbExperimentProcess
{
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int processId);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder name, ref int size);
    public static string GetImagePath(int processId)
    {
        using (SafeProcessHandle process = OpenProcess(0x1000, false, processId))
        {
            if (process.IsInvalid) { throw new Win32Exception(Marshal.GetLastWin32Error()); }
            var name = new StringBuilder(32768);
            int size = name.Capacity;
            if (!QueryFullProcessImageName(process, 0, name, ref size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return name.ToString();
        }
    }
}
'@
    }
    $Process.Refresh()
    if ($Process.HasExited) { throw [InvalidOperationException]::new('The process exited before its identity was recorded.') }
    [pscustomobject]@{
        Id = $Process.Id
        StartTimeUtc = $Process.StartTime.ToUniversalTime().ToString('o')
        # MainModule is not reliable until the child loader initializes its module list.
        Path = [MwbExperimentProcess]::GetImagePath($Process.Id)
        SessionId = $Process.SessionId
    }
}

function Test-ExperimentProcess {
    param($Record)
    $process = Get-Process -Id $Record.Id -ErrorAction SilentlyContinue
    if (-not $process) { return $false }
    try {
        $actual = Get-ExperimentProcessRecord $process
        return $actual.Path -ieq $Record.Path -and
            ([DateTime]$actual.StartTimeUtc).ToUniversalTime().Ticks -eq
                ([DateTime]$Record.StartTimeUtc).ToUniversalTime().Ticks -and
            $actual.SessionId -eq $Record.SessionId
    }
    catch [InvalidOperationException] {
        if ($process.HasExited) { return $false }
        throw
    }
    catch [ComponentModel.Win32Exception] {
        if ($process.HasExited) { return $false }
        throw
    }
    finally { $process.Dispose() }
}

function Update-EndpointProcesses {
    param([string]$EndpointRoot)
    $path = Join-Path $EndpointRoot 'processes.json'
    if (-not (Test-Path -LiteralPath $path)) { return }
    $state = Read-ExperimentJson $path
    $all = @(Get-CimInstance Win32_Process)
    do {
        $added = $false
        foreach ($candidate in $all) {
            if (-not $candidate.ExecutablePath -or
                -not $candidate.ExecutablePath.StartsWith($state.ProductRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or
                @($state.Processes | Where-Object {
                    $_.Id -eq $candidate.ProcessId -and (Test-ExperimentProcess $_)
                }).Count) { continue }
            $parent = @($state.Processes | Where-Object {
                $_.Id -eq $candidate.ParentProcessId -and (Test-ExperimentProcess $_)
            })
            if ($parent.Count -ne 1) { continue }
            $process = Get-Process -Id $candidate.ProcessId -ErrorAction SilentlyContinue
            if (-not $process) { continue }
            try { $record = Get-ExperimentProcessRecord $process }
            catch [InvalidOperationException] {
                if ($process.HasExited) { continue }
                throw
            }
            catch [ComponentModel.Win32Exception] {
                if ($process.HasExited) { continue }
                throw
            }
            finally { $process.Dispose() }
            if ($record.Path -ine $candidate.ExecutablePath -or
                [Math]::Abs((([DateTime]$record.StartTimeUtc).ToUniversalTime() -
                    $candidate.CreationDate.ToUniversalTime()).TotalMilliseconds) -gt 1 -or
                $record.SessionId -ne $parent[0].SessionId -or
                ([DateTime]$record.StartTimeUtc).ToUniversalTime() -lt
                    ([DateTime]$parent[0].StartTimeUtc).ToUniversalTime()) { continue }
            $state.Processes = @($state.Processes) + $record
            $added = $true
        }
    } while ($added)
    Write-ExperimentJson $path $state
}

function Backup-EndpointSettings {
    param([string]$EndpointRoot, [string]$SettingsRoot)
    $backupRoot = Join-Path $EndpointRoot 'backup'
    $manifestPath = Join-Path $backupRoot 'manifest.json'
    if (Test-Path -LiteralPath $backupRoot) { throw "Backup already exists: $backupRoot" }
    $null = New-Item -ItemType Directory -Path $backupRoot
    $files = @()
    foreach ($relative in @('settings.json', 'oobe_settings.json', 'MouseWithoutBorders\settings.json')) {
        $path = Join-Path $SettingsRoot $relative
        $existed = Test-Path -LiteralPath $path -PathType Leaf
        $backup = Join-Path $backupRoot ($files.Count.ToString() + '.bin')
        $hash = $null
        if ($existed) {
            [IO.File]::WriteAllBytes($backup, [IO.File]::ReadAllBytes($path))
            $hash = (Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
        }
        $files += [ordered]@{ RelativePath = $relative; Existed = $existed; Backup = $backup; Sha256 = $hash }
    }
    Write-ExperimentJson $manifestPath ([ordered]@{
        Identity = Get-ExperimentIdentity
        SettingsRoot = $SettingsRoot
        Files = $files
    })
}

function Restore-EndpointSettings {
    param([string]$EndpointRoot)
    $path = Join-Path $EndpointRoot 'backup\manifest.json'
    if (-not (Test-Path -LiteralPath $path)) { return }
    $manifest = Read-ExperimentJson $path
    $identity = Get-ExperimentIdentity
    if ($identity.ComputerName -ine $manifest.Identity.ComputerName -or $identity.UserSid -ne $manifest.Identity.UserSid) {
        throw 'Settings recovery must run as the original user on the original endpoint.'
    }
    foreach ($file in $manifest.Files) {
        if ($file.Existed -and (Get-FileHash -LiteralPath $file.Backup -Algorithm SHA256).Hash -ne $file.Sha256) {
            throw "Backup integrity failure: $($file.Backup). Originals have not been overwritten."
        }
    }
    foreach ($file in $manifest.Files) {
        $target = Join-Path $manifest.SettingsRoot $file.RelativePath
        if ($file.Existed) {
            $null = New-Item -ItemType Directory -Path (Split-Path $target) -Force
            [IO.File]::WriteAllBytes($target, [IO.File]::ReadAllBytes($file.Backup))
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $file.Sha256) {
                throw "Restoration verification failed: $target"
            }
        }
        elseif (Test-Path -LiteralPath $target) {
            Remove-Item -LiteralPath $target -Force
        }
    }
}

function Start-MwbEndpoint {
    param([string]$EndpointRoot, [string]$ProductRoot, [string[]]$ModuleNames,
        [string]$PeerName, [string]$PeerAddress, [switch]$AllowNonConsole)
    $desktop = Assert-ExperimentDesktop -AllowNonConsole:$AllowNonConsole
    if (-not $AllowNonConsole) { throw 'This launcher requires explicit Debug experiment consent.' }
    Assert-NoPowerToys
    if ($PeerName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]*$' -or
        ([Net.IPAddress]::Parse($PeerAddress)).AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) {
        throw 'A DNS peer name and IPv4 address are required.'
    }
    $settingsRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys'
    $null = New-Item -ItemType Directory -Path $EndpointRoot -Force
    Backup-EndpointSettings $EndpointRoot $settingsRoot
    Write-ExperimentJson (Join-Path $EndpointRoot 'desktop.json') $desktop
    $null = New-Item -ItemType Directory -Path "$settingsRoot\MouseWithoutBorders" -Force
    $enabled = [ordered]@{}
    foreach ($name in $ModuleNames) { $enabled[$name] = $name -eq 'MouseWithoutBorders' }
    Write-ExperimentJson "$settingsRoot\settings.json" ([ordered]@{
        startup = $false
        run_elevated = $false
        show_whats_new_after_updates = $false
        download_updates_automatically = $false
        show_new_updates_toast_notification = $false
        enable_experimentation = $false
        enabled = $enabled
    })
    Write-ExperimentJson "$settingsRoot\oobe_settings.json" @{ openedAtFirstLaunch = $true }
    Write-ExperimentJson "$settingsRoot\MouseWithoutBorders\settings.json" (New-MwbExperimentSettings $PeerName $PeerAddress)
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $ProductRoot 'PowerToys.exe'
    $start.WorkingDirectory = $ProductRoot
    $start.Arguments = '--open-settings'
    $start.UseShellExecute = $false
    $start.EnvironmentVariables['POWERTOYS_MWB_ALLOW_NONCONSOLE'] = '1'
    $process = [Diagnostics.Process]::Start($start)
    try {
        Write-ExperimentJson (Join-Path $EndpointRoot 'processes.json') ([ordered]@{
            ProductRoot = $ProductRoot
            Processes = @(Get-ExperimentProcessRecord $process)
        })
    }
    catch {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        throw
    }
}

function New-MwbExperimentSettings {
    param([string]$PeerName, [string]$PeerAddress)
    $settings = Read-ExperimentJson (Join-Path $PSScriptRoot 'MwbSettings.template.json')
    $settings.properties.Name2IP.value = "$PeerName $PeerAddress"
    $settings
}

function Stop-MwbEndpoint {
    param([string]$EndpointRoot)
    if (Test-Path -LiteralPath (Join-Path $EndpointRoot 'restored.json')) { return }
    Update-EndpointProcesses $EndpointRoot
    $path = Join-Path $EndpointRoot 'processes.json'
    if (Test-Path -LiteralPath $path) {
        $state = Read-ExperimentJson $path
        # Stop Runner first so it cannot restart a child during cleanup.
        foreach ($record in $state.Processes) {
            if (Test-ExperimentProcess $record) {
                try { Stop-Process -Id $record.Id -Force }
                catch { if (Test-ExperimentProcess $record) { throw } }
                Wait-Process -Id $record.Id -Timeout 15 -ErrorAction SilentlyContinue
                if (Test-ExperimentProcess $record) { throw "Owned PID $($record.Id) did not stop." }
            }
        }
    }
    # Late-starting children may exit asynchronously after their Runner has stopped.
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $remaining = @(Get-Process | Where-Object { $_.ProcessName -match '^PowerToys($|\.)' })
        if (-not $remaining.Count) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    Assert-NoPowerToys
    Restore-EndpointSettings $EndpointRoot
    if (Test-Path -LiteralPath $EndpointRoot) {
        Write-ExperimentJson (Join-Path $EndpointRoot 'restored.json') @{ TimestampUtc = [DateTime]::UtcNow.ToString('o') }
    }
}

function Get-EndpointSettingsProcess {
    param([string]$EndpointRoot, [switch]$AllowStarting)
    $state = Read-ExperimentJson (Join-Path $EndpointRoot 'processes.json')
    $settings = @($state.Processes | Where-Object {
        [IO.Path]::GetFileName($_.Path) -ieq 'PowerToys.Settings.exe' -and (Test-ExperimentProcess $_)
    })
    $runner = @($state.Processes | Where-Object {
        [IO.Path]::GetFileName($_.Path) -ieq 'PowerToys.exe' -and (Test-ExperimentProcess $_)
    })
    $mwb = @($state.Processes | Where-Object {
        [IO.Path]::GetFileName($_.Path) -ieq 'PowerToys.MouseWithoutBorders.exe' -and (Test-ExperimentProcess $_)
    })
    if ($runner.Count -ne 1 -or $settings.Count -gt 1 -or $mwb.Count -gt 1) {
        throw 'Missing Runner or ambiguous endpoint ownership. Inspect endpoint evidence.'
    }
    if ($settings.Count -eq 0 -or $mwb.Count -eq 0) {
        if ($AllowStarting) { return }
        throw 'The owned MWB and Settings processes have not both started.'
    }
    $settings[0].Id
}

function Get-EndpointEvidence {
    param([string]$EndpointRoot)
    $state = Read-ExperimentJson (Join-Path $EndpointRoot 'processes.json')
    $owned = @($state.Processes | Where-Object { Test-ExperimentProcess $_ })
    [ordered]@{
        TimestampUtc = [DateTime]::UtcNow.ToString('o')
        Desktop = Get-ExperimentDesktop
        Processes = $owned
        Connections = @(Get-NetTCPConnection -ErrorAction Stop |
            Where-Object { $_.OwningProcess -in @($owned | ForEach-Object { $_.Id }) } |
            Select-Object LocalAddress, LocalPort, RemoteAddress, RemotePort, State, OwningProcess)
    }
}

function Invoke-ExperimentWsb {
    param([string[]]$Arguments)
    if ($Arguments[0] -in 'start', 'connect') {
        # Waiting on a PowerShell native pipeline also waits for the long-lived Sandbox viewer.
        $start = New-Object Diagnostics.ProcessStartInfo
        $start.FileName = (Get-Command wsb.exe -ErrorAction Stop).Source
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.Arguments = ConvertTo-ExperimentArguments (@($Arguments) + '--raw')
        $process = [Diagnostics.Process]::Start($start)
        try {
            if (-not $process.WaitForExit(180000)) {
                Stop-Process -Id $process.Id -Force
                throw "wsb $($Arguments[0]) did not return within 180 seconds."
            }
            if ($process.ExitCode -ne 0) { throw "wsb $($Arguments[0]) failed ($($process.ExitCode))." }
        }
        finally { $process.Dispose() }
        return
    }
    $raw = & wsb.exe @Arguments --raw
    if ($LASTEXITCODE -ne 0) { throw "wsb $($Arguments[0]) failed ($LASTEXITCODE): $raw" }
    $raw | Out-String | ConvertFrom-Json
}

function ConvertTo-ExperimentArguments {
    param([string[]]$Arguments)
    (@($Arguments | ForEach-Object {
        $escaped = [regex]::Replace($_, '(\\*)"', '$1$1\"')
        $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
        '"' + $escaped + '"'
    })) -join ' '
}

function Get-ExperimentSandboxIds {
    param($Inventory)
    foreach ($entry in $Inventory.WindowsSandboxEnvironments) {
        $value = if ($entry -is [string]) { $entry }
        elseif ($entry.PSObject.Properties['Id']) { $entry.Id }
        elseif ($entry.PSObject.Properties['SandboxId']) { $entry.SandboxId }
        else { throw 'Unrecognized wsb list schema. Refusing to guess Sandbox ownership.' }
        ([Guid]::Parse($value)).ToString()
    }
}
