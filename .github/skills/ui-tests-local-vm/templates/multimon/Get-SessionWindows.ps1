# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Records every visible top-level window and every console-related process in the interactive session.

.DESCRIPTION
Runs inside the guest in the standard user's interactive session, under PowerShell 7, launched by a
limited interactive scheduled task. It explains windows that appear in a screenshot without anyone
opening them: for each visible top-level window it records the title, class, owner, bounds, cloaked
and minimized state, the owning process with its command line and creation time, and that process's
parent chain. It also records every console host, terminal, and shell process in the session, since a
console window handed to Windows Terminal belongs to WindowsTerminal.exe rather than to the program
that asked for the console. The probe's own process and parent chain are recorded too, so a window
the probe itself caused is recognizable.

The Start and Search surfaces are recorded separately, hidden and cloaked ones included: every
top-level Windows.UI.Core.CoreWindow window and every window of StartMenuExperienceHost.exe or
SearchHost.exe. StartOpen and SearchOpen say whether a visible, uncloaked one of those processes was on
screen at the snapshot, or at the last sample of a watch.

Command lines of processes owned by other accounts are not readable by a standard user and come back
empty.

.PARAMETER Label
Name of the point in a run this snapshot belongs to, for example after-layout.

.PARAMETER OutputPath
JSON file that receives the snapshot or the watch record.

.PARAMETER WatchSeconds
Zero takes one snapshot. A positive value samples every 250 ms for up to that long and records every
window and console process seen at any point, with first-seen and last-seen times, so windows that
exist only while another task runs are caught.

.PARAMETER StopFile
In watch mode, a file whose appearance ends the watch early.

.EXAMPLE
pwsh -NoProfile -File C:\PowerToysUiTestRun\Multimon\Get-SessionWindows.ps1 -Label before -OutputPath C:\PowerToysUiTestRun\Multimon\out\windows-01-before.json

.EXAMPLE
pwsh -NoProfile -File C:\PowerToysUiTestRun\Multimon\Get-SessionWindows.ps1 -Label run -WatchSeconds 600 -StopFile C:\PowerToysUiTestRun\Multimon\diag\stop -OutputPath C:\PowerToysUiTestRun\Multimon\diag\windows-watch.json
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9-]+$')]
    [string]$Label,
    [Parameter(Mandatory)]
    [string]$OutputPath,
    [ValidateRange(0, 3600)]
    [int]$WatchSeconds = 0,
    [string]$StopFile = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace PowerToysUiTestVm
{
    public sealed class TopLevelWindow
    {
        public long Handle;
        public string Title = "";
        public string ClassName = "";
        public int ProcessId;
        public int ThreadId;
        public long Owner;
        public bool Visible;
        public bool Cloaked;
        public bool Minimized;
        public bool ToolWindow;
        public bool Foreground;
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static class WindowProbe
    {
        private const uint GW_OWNER = 4;
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOOLWINDOW = 0x00000080;
        private const int DWMWA_CLOAKED = 14;
        private const string CORE_WINDOW_CLASS = "Windows.UI.Core.CoreWindow";

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowExW(IntPtr parent, IntPtr childAfter, string className, string windowName);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLengthW(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassNameW(IntPtr window, StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint command);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtrW(IntPtr window, int index);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

        public static List<TopLevelWindow> GetVisibleWindows()
        {
            List<TopLevelWindow> windows = new List<TopLevelWindow>();
            IntPtr foreground = GetForegroundWindow();
            EnumWindowsProc callback = delegate (IntPtr window, IntPtr parameter)
            {
                if (IsWindowVisible(window))
                {
                    windows.Add(Describe(window, foreground));
                }

                return true;
            };

            if (!EnumWindows(callback, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "EnumWindows failed");
            }

            GC.KeepAlive(callback);
            return windows;
        }

        // Start and Search draw in CoreWindow-class windows that stay visible and are cloaked while
        // closed. EnumWindows enumerates only desktop-app windows on Windows 8 and later, so this
        // walks every top-level window with FindWindowEx and keeps the CoreWindow-class windows plus any
        // window owned by one of the given processes, hidden and cloaked ones included.
        public static List<TopLevelWindow> GetShellSurfaces(int[] processIds)
        {
            HashSet<int> shellProcesses = new HashSet<int>(processIds);
            List<TopLevelWindow> windows = new List<TopLevelWindow>();
            IntPtr foreground = GetForegroundWindow();
            IntPtr window = IntPtr.Zero;
            while ((window = FindWindowExW(IntPtr.Zero, window, null, null)) != IntPtr.Zero)
            {
                StringBuilder className = new StringBuilder(256);
                GetClassNameW(window, className, className.Capacity);
                uint processId;
                GetWindowThreadProcessId(window, out processId);
                if (className.ToString() == CORE_WINDOW_CLASS || shellProcesses.Contains((int)processId))
                {
                    windows.Add(Describe(window, foreground));
                }
            }

            return windows;
        }

        private static TopLevelWindow Describe(IntPtr window, IntPtr foreground)
        {
            TopLevelWindow entry = new TopLevelWindow();
            entry.Handle = window.ToInt64();

            int length = GetWindowTextLengthW(window);
            StringBuilder title = new StringBuilder(Math.Max(length, 0) + 1);
            GetWindowTextW(window, title, title.Capacity);
            entry.Title = title.ToString();

            StringBuilder className = new StringBuilder(256);
            GetClassNameW(window, className, className.Capacity);
            entry.ClassName = className.ToString();

            uint processId;
            entry.ThreadId = (int)GetWindowThreadProcessId(window, out processId);
            entry.ProcessId = (int)processId;
            entry.Owner = GetWindow(window, GW_OWNER).ToInt64();
            entry.Visible = IsWindowVisible(window);
            entry.Minimized = IsIconic(window);
            entry.ToolWindow = (GetWindowLongPtrW(window, GWL_EXSTYLE).ToInt64() & WS_EX_TOOLWINDOW) != 0;
            entry.Foreground = window == foreground;

            int cloaked;
            entry.Cloaked = DwmGetWindowAttribute(window, DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0 && cloaked != 0;

            RECT rect;
            if (GetWindowRect(window, out rect))
            {
                entry.Left = rect.Left;
                entry.Top = rect.Top;
                entry.Right = rect.Right;
                entry.Bottom = rect.Bottom;
            }

            return entry;
        }
    }
}
'@

# A diagnostic records what it can, so a thread left unaware is reported in DpiAwareness rather than
# refused; its bounds are then scaled.
$awareness = & (Join-Path $PSScriptRoot 'Enable-PerMonitorDpiAwareness.ps1') -ReportOnly
$sessionId = (Get-Process -Id $PID).SessionId
$processes = @{}

function Read-ProcessTable {
    $script:processes = @{}
    foreach ($process in Get-CimInstance -ClassName Win32_Process) {
        $script:processes[[int]$process.ProcessId] = $process
    }
}

Read-ProcessTable

function Get-ProcessSummary {
    param([Parameter(Mandatory)][int]$ProcessId)

    $process = $processes[$ProcessId]
    if ($null -eq $process) {
        return [pscustomobject]@{ ProcessId = $ProcessId; Name = '<exited>'; SessionId = $null; Created = $null; CommandLine = $null }
    }
    [pscustomobject]@{
        ProcessId   = $ProcessId
        Name        = [string]$process.Name
        SessionId   = [int]$process.SessionId
        Created     = ${process}?.CreationDate?.ToString('o')
        CommandLine = [string]$process.CommandLine
    }
}

# Walks to the root of the parent chain. A parent created after its child is a reused process ID, not
# the real parent, so the walk stops there.
function Get-ParentChain {
    param([Parameter(Mandatory)][int]$ProcessId)

    $chain = [System.Collections.Generic.List[object]]::new()
    $seen = [System.Collections.Generic.HashSet[int]]::new()
    $current = $processes[$ProcessId]
    while ($null -ne $current -and $chain.Count -lt 12) {
        $parentId = [int]$current.ParentProcessId
        if (-not $seen.Add($parentId) -or $parentId -eq 0) {
            break
        }
        $parent = $processes[$parentId]
        if ($null -eq $parent) {
            $chain.Add([pscustomobject]@{ ProcessId = $parentId; Name = '<exited>'; Created = $null })
            break
        }
        if ($null -ne $parent.CreationDate -and $null -ne $current.CreationDate -and $parent.CreationDate -gt $current.CreationDate) {
            $reused = "<reused by $($parent.Name)>"
            $chain.Add([pscustomobject]@{ ProcessId = $parentId; Name = $reused; Created = $parent.CreationDate.ToString('o') })
            break
        }
        $created = ${parent}?.CreationDate?.ToString('o')
        $chain.Add([pscustomobject]@{ ProcessId = $parentId; Name = [string]$parent.Name; Created = $created })
        $current = $parent
    }
    return $chain
}

$consoleNames = @('pwsh.exe', 'powershell.exe', 'cmd.exe', 'conhost.exe', 'OpenConsole.exe', 'WindowsTerminal.exe', 'wt.exe')

function ConvertTo-WindowRecord {
    param([Parameter(Mandatory)][object]$Window)

    [pscustomobject]@{
        Handle      = '0x{0:X}' -f $Window.Handle
        Title       = $Window.Title
        ClassName   = $Window.ClassName
        Bounds      = "$($Window.Left),$($Window.Top) to $($Window.Right),$($Window.Bottom)"
        Visible     = $Window.Visible
        Cloaked     = $Window.Cloaked
        Minimized   = $Window.Minimized
        ToolWindow  = $Window.ToolWindow
        Foreground  = $Window.Foreground
        Owner       = '0x{0:X}' -f $Window.Owner
        ThreadId    = $Window.ThreadId
        Process     = Get-ProcessSummary -ProcessId $Window.ProcessId
        ParentChain = Get-ParentChain -ProcessId $Window.ProcessId
    }
}

function Get-ConsoleProcessRecord {
    @($processes.Values |
            Where-Object { $_.Name -in $consoleNames -and [int]$_.SessionId -eq $sessionId } |
            Sort-Object -Property CreationDate |
            ForEach-Object {
                [pscustomobject]@{
                    Process     = Get-ProcessSummary -ProcessId ([int]$_.ProcessId)
                    ParentChain = Get-ParentChain -ProcessId ([int]$_.ProcessId)
                }
            })
}

$shellNames = @('StartMenuExperienceHost.exe', 'SearchHost.exe')

function Get-ShellSurface {
    $shellIds = @($processes.Values |
            Where-Object { $_.Name -in $shellNames -and [int]$_.SessionId -eq $sessionId } |
            ForEach-Object { [int]$_.ProcessId })
    @([PowerToysUiTestVm.WindowProbe]::GetShellSurfaces([int[]]$shellIds))
}

# A shell surface is open on screen when its window is visible and not cloaked.
function Test-SurfaceOpen {
    param(
        [object[]]$Surfaces,
        [Parameter(Mandatory)]
        [string]$ProcessName
    )

    @($Surfaces | Where-Object {
            $_.Visible -and -not $_.Cloaked -and $processes[$_.ProcessId]?.Name -eq $ProcessName
        }).Count -gt 0
}

$self = [pscustomobject]@{
    Process     = Get-ProcessSummary -ProcessId $PID
    ParentChain = Get-ParentChain -ProcessId $PID
}

$result = $null
if ($WatchSeconds -eq 0) {
    $surfaces = @(Get-ShellSurface)
    $result = [pscustomobject]@{
        Mode             = 'snapshot'
        Label            = $Label
        CapturedAt       = (Get-Date).ToString('o')
        SessionId        = $sessionId
        DpiAwareness     = $awareness
        StartOpen        = Test-SurfaceOpen -Surfaces $surfaces -ProcessName 'StartMenuExperienceHost.exe'
        SearchOpen       = Test-SurfaceOpen -Surfaces $surfaces -ProcessName 'SearchHost.exe'
        Self             = $self
        Windows          = @([PowerToysUiTestVm.WindowProbe]::GetVisibleWindows() | ForEach-Object { ConvertTo-WindowRecord -Window $_ })
        ShellSurfaces    = @($surfaces | ForEach-Object { ConvertTo-WindowRecord -Window $_ })
        ConsoleProcesses = Get-ConsoleProcessRecord
    }
}
else {
    # A window that lives only while one step runs is gone before a snapshot after that step, so watch
    # mode samples continuously and keeps every window and console process it ever saw, with the first
    # and last time it was seen. Process details are read when a window or process first appears,
    # because the process may have exited by the end of the watch.
    $seenWindows = [System.Collections.Specialized.OrderedDictionary]::new()
    $seenProcesses = [System.Collections.Specialized.OrderedDictionary]::new()
    # Start and Search keep one window and toggle its state, so each state of a surface is its own entry.
    $seenSurfaces = [System.Collections.Specialized.OrderedDictionary]::new()
    $surfaces = @()
    $watchStarted = Get-Date
    $watchDeadline = $watchStarted.AddSeconds($WatchSeconds)
    $nextProcessRefresh = $watchStarted
    $samples = 0
    do {
        $now = (Get-Date).ToString('o')
        $current = @([PowerToysUiTestVm.WindowProbe]::GetVisibleWindows())
        $unseen = @($current | Where-Object { -not $seenWindows.Contains($_.Handle) }).Count -gt 0
        if ($unseen -or (Get-Date) -ge $nextProcessRefresh) {
            Read-ProcessTable
            $nextProcessRefresh = (Get-Date).AddSeconds(1)
            foreach ($record in Get-ConsoleProcessRecord) {
                $key = "$($record.Process.ProcessId)@$($record.Process.Created)"
                if ($seenProcesses.Contains($key)) {
                    $seenProcesses[$key].LastSeen = $now
                }
                else {
                    $seenProcesses[$key] = [pscustomobject]@{
                        FirstSeen   = $now
                        LastSeen    = $now
                        Process     = $record.Process
                        ParentChain = $record.ParentChain
                    }
                }
            }
        }
        foreach ($window in $current) {
            if ($seenWindows.Contains($window.Handle)) {
                $seenWindows[$window.Handle].LastSeen = $now
            }
            else {
                $seenWindows[$window.Handle] = [pscustomobject]@{
                    FirstSeen      = $now
                    LastSeen       = $now
                    PresentAtStart = $samples -eq 0
                    Window         = ConvertTo-WindowRecord -Window $window
                }
            }
        }
        $surfaces = @(Get-ShellSurface)
        foreach ($surface in $surfaces) {
            $key = "$($surface.Handle)/$($surface.Visible)/$($surface.Cloaked)"
            if ($seenSurfaces.Contains($key)) {
                $seenSurfaces[$key].LastSeen = $now
            }
            else {
                $seenSurfaces[$key] = [pscustomobject]@{
                    FirstSeen      = $now
                    LastSeen       = $now
                    PresentAtStart = $samples -eq 0
                    Window         = ConvertTo-WindowRecord -Window $surface
                }
            }
        }
        $samples++
        if (-not [string]::IsNullOrWhiteSpace($StopFile) -and (Test-Path -LiteralPath $StopFile -PathType Leaf)) {
            break
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $watchDeadline)

    $result = [pscustomobject]@{
        Mode             = 'watch'
        Label            = $Label
        StartedAt        = $watchStarted.ToString('o')
        EndedAt          = (Get-Date).ToString('o')
        Samples          = $samples
        SessionId        = $sessionId
        DpiAwareness     = $awareness
        StartOpen        = Test-SurfaceOpen -Surfaces $surfaces -ProcessName 'StartMenuExperienceHost.exe'
        SearchOpen       = Test-SurfaceOpen -Surfaces $surfaces -ProcessName 'SearchHost.exe'
        Self             = $self
        Windows          = @($seenWindows.Values)
        ShellSurfaces    = @($seenSurfaces.Values)
        ConsoleProcesses = @($seenProcesses.Values)
    }
}

New-Item -ItemType Directory -Path (Split-Path -Path $OutputPath -Parent) -Force | Out-Null
$result | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $OutputPath -Encoding utf8

[pscustomobject]@{
    Label            = $Label
    Mode             = $result.Mode
    StartOpen        = $result.StartOpen
    SearchOpen       = $result.SearchOpen
    Windows          = @($result.Windows).Count
    ShellSurfaces    = @($result.ShellSurfaces).Count
    ConsoleProcesses = @($result.ConsoleProcesses).Count
    OutputPath       = $OutputPath
}
