<#
.SYNOPSIS
Measures startup time and memory of the PowerToys runner and of the .NET apps that ship as
published builds: Settings, PowerToys Run, File Locksmith, and the .NET preview handlers.

.DESCRIPTION
Every sample starts a new process from -PowerToysRoot. The warm-up samples run first, so the files
are already in the OS file cache: the numbers are "new process, warm disk cache" startup, which is
what JIT, ReadyToRun, and AOT changes move. A cold boot isn't simulated.

Scenarios and what they report (times in ms from launch unless noted):
  Runner          The stage times the runner logs ("Startup stages" line, ms since process
                  creation), the runner's memory, and the total working set of every process
                  started from -PowerToysRoot. Needs a build whose runner logs its stages.
  Settings        Opens Settings through the running runner (PowerToys.exe --open-settings=Dashboard).
                  WindowShownMs: the Settings window is shown. ShellReadyMs: the "DashboardNavItem"
                  navigation item is found through UI Automation.
  PowerToysRun    Starts PowerToys.PowerLauncher.exe on its own. InputIdleMs: the UI thread goes
                  idle after startup, which is after the plugins are loaded.
  FileLocksmith   Starts the File Locksmith UI for one sample file. WindowShownMs, then UiReadyMs:
                  "ReloadBtn" is found through UI Automation.
  MarkdownPreview, MonacoPreview, SvgPreview
                  Starts the preview handler like File Explorer's shim does, hosted in a window that
                  this script creates. WindowShownMs: the handler's window is shown in the host.
                  WebViewShownMs: the WebView2 browser process shows its window in the host. What
                  follows (navigation and first paint) is native WebView2 work.
  SvgThumbnail    Starts the SVG thumbnail provider like the shim does. ExitMs: the bitmap is
                  written and the process has exited.

Memory (WorkingSetMB, PrivateMB) is sampled -SettleMilliseconds after the last startup milestone.

Runner, Settings, and PowerToysRun need PowerToys to themselves: the script stops every running
PowerToys runner first and starts those again at the end. Local builds and installed builds share
%LOCALAPPDATA%\Microsoft\PowerToys, and a build of another version rewrites files there, so for
these scenarios the script copies that folder (without logs) first and puts it back at the end.
It writes the measured build's version to last_version_run.json so that "What's new" doesn't open.
The other scenarios leave a running PowerToys alone; FileLocksmith restores the last-run.log file
it uses.

.PARAMETER PowerToysRoot
Folder that contains PowerToys.exe, for example a local build output (x64\Release) or an install folder.

.PARAMETER Scenario
Scenarios to run. Defaults to all of them.

.PARAMETER Iterations
Measured samples per scenario.

.PARAMETER WarmupIterations
Samples per scenario that run first and are left out of the summary.

.PARAMETER SettleMilliseconds
Wait after the last startup milestone before sampling memory.

.PARAMETER Label
Name for this run. It's used in the result file name and by Compare-StartupPerformance.ps1.

.PARAMETER OutputDirectory
Folder for the JSON result file. The copy of the PowerToys data folder is kept here during the run,
and stays here if it can't be put back.

.EXAMPLE
.\Measure-StartupPerformance.ps1 -PowerToysRoot C:\src\PowerToys\x64\Release -Label main

.EXAMPLE
.\Measure-StartupPerformance.ps1 -PowerToysRoot 'C:\Program Files\PowerToys' -Scenario Settings,MarkdownPreview -Iterations 20
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PowerToysRoot,

    [ValidateSet('Runner', 'Settings', 'PowerToysRun', 'FileLocksmith', 'MarkdownPreview', 'MonacoPreview', 'SvgPreview', 'SvgThumbnail')]
    [string[]]$Scenario = @('Runner', 'Settings', 'PowerToysRun', 'FileLocksmith', 'MarkdownPreview', 'MonacoPreview', 'SvgPreview', 'SvgThumbnail'),

    [ValidateRange(1, 200)]
    [int]$Iterations = 10,

    [ValidateRange(0, 20)]
    [int]$WarmupIterations = 2,

    [ValidateRange(0, 60000)]
    [int]$SettleMilliseconds = 3000,

    [string]$Label = 'run',

    [string]$OutputDirectory = (Join-Path $env:TEMP 'PowerToys-Startup-Performance')
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

if (-not ('PowerToysPerformance.WindowShowRecorder' -as [type]))
{
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PowerToysPerformance
{
    public sealed class WindowShowEvent
    {
        public long Timestamp { get; set; }
        public IntPtr Handle { get; set; }
        public int ProcessId { get; set; }
        public IntPtr Root { get; set; }
        public string ClassName { get; set; }
    }

    // Records EVENT_OBJECT_SHOW for all windows on its own thread, so the time a window appears is
    // taken when Windows reports it rather than when a polling loop happens to notice it.
    public sealed class WindowShowRecorder : IDisposable
    {
        private readonly object sync = new object();
        private readonly List<WindowShowEvent> events = new List<WindowShowEvent>();
        private readonly ManualResetEvent started = new ManualResetEvent(false);
        private readonly Thread thread;
        private NativeMethods.WinEventProc callback;
        private uint threadId;

        public WindowShowRecorder()
        {
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Start();
            started.WaitOne();
        }

        public void Clear()
        {
            lock (sync)
            {
                events.Clear();
            }
        }

        public WindowShowEvent[] GetEvents()
        {
            lock (sync)
            {
                return events.ToArray();
            }
        }

        public void Dispose()
        {
            NativeMethods.PostThreadMessage(threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            thread.Join(5000);
        }

        private void Run()
        {
            threadId = NativeMethods.GetCurrentThreadId();
            callback = OnWinEvent;
            IntPtr hook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_SHOW,
                NativeMethods.EVENT_OBJECT_SHOW,
                IntPtr.Zero,
                callback,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

            NativeMethods.MSG msg;
            NativeMethods.PeekMessage(out msg, IntPtr.Zero, 0, 0, 0);
            started.Set();

            while (NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }

            NativeMethods.UnhookWinEvent(hook);
        }

        private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint eventThread, uint eventTime)
        {
            if (hwnd == IntPtr.Zero || idObject != NativeMethods.OBJID_WINDOW || idChild != 0)
            {
                return;
            }

            long timestamp = Stopwatch.GetTimestamp();
            uint processId;
            NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
            StringBuilder className = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, className, className.Capacity);

            WindowShowEvent showEvent = new WindowShowEvent();
            showEvent.Timestamp = timestamp;
            showEvent.Handle = hwnd;
            showEvent.ProcessId = (int)processId;
            showEvent.Root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            showEvent.ClassName = className.ToString();

            lock (sync)
            {
                events.Add(showEvent);
            }
        }
    }

    // Top-level window with its own message loop. Preview handlers become child windows of it, the
    // same way they do in File Explorer's preview pane. The loop has to keep running because the
    // handler process sends messages to its parent window while it starts.
    public sealed class HostWindow : IDisposable
    {
        private readonly ManualResetEvent created = new ManualResetEvent(false);
        private readonly Thread thread;
        private uint threadId;

        public HostWindow(int width, int height)
        {
            thread = new Thread(() => Run(width, height));
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            created.WaitOne();
        }

        public IntPtr Handle { get; private set; }

        public int ClientWidth { get; private set; }

        public int ClientHeight { get; private set; }

        public void Dispose()
        {
            NativeMethods.PostThreadMessage(threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            thread.Join(5000);
        }

        private void Run(int width, int height)
        {
            threadId = NativeMethods.GetCurrentThreadId();
            IntPtr instance = NativeMethods.GetModuleHandle(null);

            NativeMethods.WNDCLASSEX windowClass = new NativeMethods.WNDCLASSEX();
            windowClass.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX));
            windowClass.lpfnWndProc = NativeMethods.GetProcAddress(NativeMethods.GetModuleHandle("user32.dll"), "DefWindowProcW");
            windowClass.hInstance = instance;
            windowClass.hbrBackground = new IntPtr(NativeMethods.COLOR_WINDOW + 1);
            windowClass.lpszClassName = "PowerToysStartupMeasurementHost";
            NativeMethods.RegisterClassEx(ref windowClass);

            Handle = NativeMethods.CreateWindowEx(
                0,
                windowClass.lpszClassName,
                "PowerToys startup measurement",
                NativeMethods.WS_OVERLAPPEDWINDOW | NativeMethods.WS_CLIPCHILDREN | NativeMethods.WS_VISIBLE,
                80,
                80,
                width,
                height,
                IntPtr.Zero,
                IntPtr.Zero,
                instance,
                IntPtr.Zero);

            NativeMethods.RECT client;
            NativeMethods.GetClientRect(Handle, out client);
            ClientWidth = client.Right - client.Left;
            ClientHeight = client.Bottom - client.Top;
            created.Set();

            NativeMethods.MSG msg;
            while (NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }

            NativeMethods.DestroyWindow(Handle);
        }
    }

    // A process held open by handle. While the handle is open, Windows can't give the process id to
    // another process, so waiting and killing always reach this process.
    public sealed class PinnedProcess : IDisposable
    {
        private IntPtr handle;

        internal PinnedProcess(int id, IntPtr handle)
        {
            Id = id;
            this.handle = handle;
        }

        public int Id { get; private set; }

        public bool WaitForExit(int milliseconds)
        {
            return NativeMethods.WaitForSingleObject(handle, (uint)Math.Max(0, milliseconds)) == NativeMethods.WAIT_OBJECT_0;
        }

        public void Kill()
        {
            NativeMethods.TerminateProcess(handle, 1);
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(handle);
                handle = IntPtr.Zero;
            }
        }
    }

    public static class ProcessTree
    {
        // Live processes that descend from the given one, including those whose parent has already
        // exited. The caller must keep the root process open. The process snapshot can be stale, so
        // each process is opened first and then checked: its parent id has to match and it has to
        // be created after its parent. Processes that can't be opened are skipped.
        public static PinnedProcess[] GetDescendants(int processId, DateTime startTimeUtc)
        {
            Dictionary<int, List<int>> children = new Dictionary<int, List<int>>();
            IntPtr snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0);
            if (snapshot == NativeMethods.INVALID_HANDLE_VALUE)
            {
                return new PinnedProcess[0];
            }

            try
            {
                NativeMethods.PROCESSENTRY32 entry = new NativeMethods.PROCESSENTRY32();
                entry.dwSize = (uint)Marshal.SizeOf(typeof(NativeMethods.PROCESSENTRY32));
                if (NativeMethods.Process32First(snapshot, ref entry))
                {
                    do
                    {
                        int parent = (int)entry.th32ParentProcessID;
                        List<int> list;
                        if (!children.TryGetValue(parent, out list))
                        {
                            list = new List<int>();
                            children[parent] = list;
                        }

                        list.Add((int)entry.th32ProcessID);
                    }
                    while (NativeMethods.Process32Next(snapshot, ref entry));
                }
            }
            finally
            {
                NativeMethods.CloseHandle(snapshot);
            }

            List<PinnedProcess> result = new List<PinnedProcess>();
            HashSet<int> seen = new HashSet<int>();
            seen.Add(processId);
            Queue<KeyValuePair<int, long>> pending = new Queue<KeyValuePair<int, long>>();
            pending.Enqueue(new KeyValuePair<int, long>(processId, startTimeUtc.ToFileTimeUtc()));
            while (pending.Count > 0)
            {
                KeyValuePair<int, long> parent = pending.Dequeue();
                List<int> list;
                if (!children.TryGetValue(parent.Key, out list))
                {
                    continue;
                }

                foreach (int child in list)
                {
                    if (seen.Contains(child))
                    {
                        continue;
                    }

                    IntPtr handle = NativeMethods.OpenProcess(
                        NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION | NativeMethods.SYNCHRONIZE | NativeMethods.PROCESS_TERMINATE,
                        false,
                        (uint)child);
                    if (handle == IntPtr.Zero)
                    {
                        continue;
                    }

                    long created;
                    long exit;
                    long kernel;
                    long user;
                    int parentId;
                    if (!NativeMethods.GetProcessTimes(handle, out created, out exit, out kernel, out user) ||
                        created < parent.Value ||
                        !TryGetParentId(handle, out parentId) ||
                        parentId != parent.Key)
                    {
                        NativeMethods.CloseHandle(handle);
                        continue;
                    }

                    seen.Add(child);
                    result.Add(new PinnedProcess(child, handle));
                    pending.Enqueue(new KeyValuePair<int, long>(child, created));
                }
            }

            return result.ToArray();
        }

        private static bool TryGetParentId(IntPtr handle, out int parentId)
        {
            parentId = 0;
            NativeMethods.PROCESS_BASIC_INFORMATION info = new NativeMethods.PROCESS_BASIC_INFORMATION();
            int returnLength;
            if (NativeMethods.NtQueryInformationProcess(handle, 0, ref info, Marshal.SizeOf(typeof(NativeMethods.PROCESS_BASIC_INFORMATION)), out returnLength) != 0)
            {
                return false;
            }

            parentId = (int)info.InheritedFromUniqueProcessId.ToInt64();
            return true;
        }
    }

    public static class WindowFinder
    {
        public static IntPtr FindTopLevelWindow(int processId, string className)
        {
            IntPtr found = IntPtr.Zero;
            NativeMethods.EnumWindows(
                (hwnd, lParam) =>
                {
                    uint windowProcessId;
                    NativeMethods.GetWindowThreadProcessId(hwnd, out windowProcessId);
                    if (windowProcessId != processId)
                    {
                        return true;
                    }

                    StringBuilder name = new StringBuilder(256);
                    NativeMethods.GetClassName(hwnd, name, name.Capacity);
                    if (string.Equals(name.ToString(), className, StringComparison.Ordinal))
                    {
                        found = hwnd;
                        return false;
                    }

                    return true;
                },
                IntPtr.Zero);
            return found;
        }

        public static bool PostClose(IntPtr hwnd)
        {
            return NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public static class NativeMethods
    {
        public const uint EVENT_OBJECT_SHOW = 0x8002;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
        public const int OBJID_WINDOW = 0;
        public const uint GA_ROOT = 2;
        public const uint WM_QUIT = 0x0012;
        public const uint WM_CLOSE = 0x0010;
        public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
        public const uint WS_CLIPCHILDREN = 0x02000000;
        public const uint WS_VISIBLE = 0x10000000;
        public const int COLOR_WINDOW = 5;
        public const uint TH32CS_SNAPPROCESS = 0x00000002;
        public const uint PROCESS_TERMINATE = 0x0001;
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const uint SYNCHRONIZE = 0x00100000;
        public const uint WAIT_OBJECT_0 = 0;
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX;
            public int ptY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        // Pointer-sized fields keep the padding of the native layout on 32-bit and 64-bit.
        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        public static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        public static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        public static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        public static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool GetProcessTimes(IntPtr hProcess, out long lpCreationTime, out long lpExitTime, out long lpKernelTime, out long lpUserTime);

        [DllImport("kernel32.dll")]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("ntdll.dll")]
        public static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);
    }
}
'@
}

$root = [IO.Path]::GetFullPath($PowerToysRoot).TrimEnd('\')
$runnerPath = Join-Path $root 'PowerToys.exe'
$settingsPath = Join-Path $root 'WinUI3Apps\PowerToys.Settings.exe'
$launcherPath = Join-Path $root 'PowerToys.PowerLauncher.exe'
$fileLocksmithPath = Join-Path $root 'WinUI3Apps\PowerToys.FileLocksmithUI.exe'
$svgThumbnailPath = Join-Path $root 'PowerToys.SvgThumbnailProvider.exe'
$previewHandlers = @{
    MarkdownPreview = @{ Path = (Join-Path $root 'PowerToys.MarkdownPreviewHandler.exe'); Sample = 'sample.md' }
    MonacoPreview = @{ Path = (Join-Path $root 'PowerToys.MonacoPreviewHandler.exe'); Sample = 'sample.json' }
    SvgPreview = @{ Path = (Join-Path $root 'PowerToys.SvgPreviewHandler.exe'); Sample = 'sample.svg' }
}

$powerToysDataFolder = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys'
$runnerLogFolder = Join-Path $powerToysDataFolder 'RunnerLogs'
$workFolder = Join-Path $OutputDirectory 'work'
$timestampFrequency = [double][Diagnostics.Stopwatch]::Frequency

# The single-instance mutexes are per session, so PowerToys of another signed-in user doesn't conflict.
$sessionId = (Get-Process -Id $PID).SessionId

$script:recorder = $null
$script:samples = New-Object System.Collections.Generic.List[object]
$script:launched = New-Object System.Collections.Generic.List[System.Diagnostics.Process]
$script:backups = @{}
$script:dataSnapshot = $null
$script:tookOver = $false
$script:stoppedRunnerPaths = New-Object System.Collections.Generic.List[string]

function Get-ElapsedMs
{
    param([long]$From, [long]$To)

    return [Math]::Round(($To - $From) * 1000.0 / $timestampFrequency, 1)
}

function Get-RootProcesses
{
    param([string]$Name = 'PowerToys*', [string]$Folder = $root)

    # Opening the handle first means the process id can't be reused while the path is checked or
    # while the caller uses the process. Dispose the processes when done with them.
    $prefix = $Folder.TrimEnd('\') + '\'
    return @(Get-Process -Name $Name -ErrorAction SilentlyContinue | Where-Object {
        $path = $null
        try
        {
            $null = $_.Handle
            $path = $_.Path
        }
        catch
        {
        }

        $path -and $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
    })
}

function Stop-Processes
{
    # Only pass processes that are already open, from Start-TargetProcess or Get-RootProcesses.
    param([AllowEmptyCollection()][Diagnostics.Process[]]$Processes, [int]$TimeoutMs = 10000)

    foreach ($process in $Processes)
    {
        try
        {
            if (-not $process.HasExited)
            {
                $process.Kill()
            }
        }
        catch
        {
        }
    }

    foreach ($process in $Processes)
    {
        try
        {
            $null = $process.WaitForExit($TimeoutMs)
        }
        catch
        {
        }
    }
}

function Wait-PinnedProcesses
{
    param([AllowEmptyCollection()][PowerToysPerformance.PinnedProcess[]]$Processes, [int]$TimeoutMs = 10000)

    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    foreach ($process in $Processes)
    {
        if (-not $process.WaitForExit([int][Math]::Max(0, $TimeoutMs - $stopwatch.ElapsedMilliseconds)))
        {
            $process.Kill()
            $null = $process.WaitForExit(5000)
        }
    }
}

function Stop-ProcessAndDescendants
{
    param([Parameter(Mandatory)][Diagnostics.Process]$Process)

    # WebView2 processes outlive a killed host for a moment. Waiting for them keeps the next sample
    # from attaching to a browser process that's still running.
    $descendants = @()
    try
    {
        $descendants = [PowerToysPerformance.ProcessTree]::GetDescendants($Process.Id, $Process.StartTime.ToUniversalTime())
    }
    catch
    {
        Write-Warning "Couldn't list the child processes of $($Process.Id): $($_.Exception.Message)"
    }

    try
    {
        Stop-Processes -Processes @($Process)
        Wait-PinnedProcesses -Processes $descendants
    }
    finally
    {
        foreach ($descendant in $descendants)
        {
            $descendant.Dispose()
        }
    }
}

function Stop-RootProcesses
{
    param([string]$Folder = $root)

    $processes = Get-RootProcesses -Folder $Folder
    Stop-Processes -Processes $processes
    foreach ($process in $processes)
    {
        $process.Dispose()
    }
}

function Stop-LaunchedProcesses
{
    foreach ($process in $script:launched)
    {
        try
        {
            if ($process.HasExited)
            {
                continue
            }

            if ([string]::Equals($process.StartInfo.FileName, $runnerPath, [StringComparison]::OrdinalIgnoreCase))
            {
                Stop-Runner -Process $process -Folder $root
            }
            else
            {
                Stop-ProcessAndDescendants -Process $process
            }
        }
        catch
        {
            Write-Warning "Couldn't stop process $($process.Id): $($_.Exception.Message)"
        }
    }
}

function Get-MemorySample
{
    param([Parameter(Mandatory)][Diagnostics.Process]$Process)

    $Process.Refresh()
    return [ordered]@{
        WorkingSetMB = [Math]::Round($Process.WorkingSet64 / 1MB, 1)
        PrivateMB = [Math]::Round($Process.PrivateMemorySize64 / 1MB, 1)
    }
}

function Wait-WindowShown
{
    param(
        [Parameter(Mandatory)][scriptblock]$Match,
        [Parameter(Mandatory)][string]$Description,
        [Diagnostics.Process]$Process,
        [int]$TimeoutMs = 60000
    )

    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    do
    {
        foreach ($showEvent in $script:recorder.GetEvents())
        {
            if (& $Match $showEvent)
            {
                return $showEvent
            }
        }

        if ($null -ne $Process -and $Process.HasExited)
        {
            throw "$Description wasn't shown: the process exited with code $($Process.ExitCode)."
        }

        Start-Sleep -Milliseconds 5
    }
    while ($stopwatch.ElapsedMilliseconds -lt $TimeoutMs)

    throw "$Description wasn't shown within $TimeoutMs ms."
}

function Wait-AutomationElement
{
    param(
        [Parameter(Mandatory)][IntPtr]$WindowHandle,
        [Parameter(Mandatory)][string]$AutomationId,
        [int]$TimeoutMs = 60000
    )

    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    do
    {
        try
        {
            $window = [System.Windows.Automation.AutomationElement]::FromHandle($WindowHandle)
            if ($null -ne $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition))
            {
                return [Diagnostics.Stopwatch]::GetTimestamp()
            }
        }
        catch
        {
            # The UI Automation tree isn't available while the window is still being created.
        }

        Start-Sleep -Milliseconds 15
    }
    while ($stopwatch.ElapsedMilliseconds -lt $TimeoutMs)

    throw "UI Automation element '$AutomationId' wasn't found within $TimeoutMs ms."
}

function Start-TargetProcess
{
    param([Parameter(Mandatory)][string]$Path, [string]$Arguments = '')

    $startInfo = [Diagnostics.ProcessStartInfo]::new($Path, $Arguments)
    $startInfo.UseShellExecute = $false
    $startInfo.WorkingDirectory = Split-Path $Path -Parent
    $process = [Diagnostics.Process]::Start($startInfo)
    $script:launched.Add($process)
    return $process
}

function Add-Sample
{
    param([string]$Name, [int]$Iteration, [System.Collections.IDictionary]$Metrics)

    $sample = [ordered]@{
        Scenario = $Name
        Iteration = $Iteration
        IsWarmup = $Iteration -le $WarmupIterations
    }
    foreach ($key in $Metrics.Keys)
    {
        $sample[$key] = $Metrics[$key]
    }

    $script:samples.Add([pscustomobject]$sample)
    $text = ($Metrics.GetEnumerator() | ForEach-Object { '{0}={1}' -f $_.Key, $_.Value }) -join ' '
    $phase = if ($sample.IsWarmup) { 'warm-up' } else { 'sample' }
    Write-Host ('{0} {1} {2}/{3}: {4}' -f $Name, $phase, $Iteration, ($WarmupIterations + $Iterations), $text)
}

function Backup-File
{
    param([Parameter(Mandatory)][string]$Path)

    if ($script:backups.ContainsKey($Path))
    {
        return
    }

    $content = $null
    if (Test-Path -LiteralPath $Path)
    {
        $content = [IO.File]::ReadAllBytes($Path)
    }

    $script:backups[$Path] = $content
}

function Restore-Files
{
    foreach ($path in $script:backups.Keys)
    {
        try
        {
            if ($null -eq $script:backups[$path])
            {
                Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            }
            else
            {
                [IO.File]::WriteAllBytes($path, $script:backups[$path])
            }
        }
        catch
        {
            Write-Warning "Couldn't restore $path`: $($_.Exception.Message)"
        }
    }
}

function Test-LogFile
{
    param([Parameter(Mandatory)][string]$RelativePath)

    $parts = $RelativePath.Split('\')
    for ($i = 0; $i -lt $parts.Count - 1; $i++)
    {
        if ($parts[$i] -in 'Logs', 'LogsModuleInterface', 'RunnerLogs', 'UpdateLogs', 'etw')
        {
            return $true
        }
    }

    return $parts[-1] -match '(\.etl|\d{4}-\d{2}-\d{2}\.(log|txt))$'
}

function Save-DataFolder
{
    # A build of another version rewrites shared files: version stamps in settings.json and
    # UpdateState.json, PowerToys Run's plugin data, default settings of modules. Everything but the
    # logs is copied here and put back at the end.
    $snapshot = [pscustomobject]@{
        Path = Join-Path $OutputDirectory ('data-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
        Files = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        Folders = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    }

    if (Test-Path -LiteralPath $powerToysDataFolder)
    {
        foreach ($item in Get-ChildItem -LiteralPath $powerToysDataFolder -Recurse -Force)
        {
            $relative = $item.FullName.Substring($powerToysDataFolder.Length + 1)
            if ($item.PSIsContainer)
            {
                $null = $snapshot.Folders.Add($relative)
            }
            elseif (-not (Test-LogFile -RelativePath $relative))
            {
                $target = Join-Path $snapshot.Path $relative
                $null = New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent)
                Copy-Item -LiteralPath $item.FullName -Destination $target
                $null = $snapshot.Files.Add($relative)
            }
        }
    }

    $script:dataSnapshot = $snapshot
}

function Restore-DataFolder
{
    $snapshot = $script:dataSnapshot
    $failures = 0

    if (Test-Path -LiteralPath $powerToysDataFolder)
    {
        foreach ($file in @(Get-ChildItem -LiteralPath $powerToysDataFolder -Recurse -File -Force))
        {
            $relative = $file.FullName.Substring($powerToysDataFolder.Length + 1)
            if (-not $snapshot.Files.Contains($relative) -and -not (Test-LogFile -RelativePath $relative))
            {
                try
                {
                    Remove-Item -LiteralPath $file.FullName -Force
                }
                catch
                {
                    $failures++
                    Write-Warning "Couldn't delete $($file.FullName): $($_.Exception.Message)"
                }
            }
        }
    }

    foreach ($relative in $snapshot.Files)
    {
        $source = Join-Path $snapshot.Path $relative
        $target = Join-Path $powerToysDataFolder $relative
        try
        {
            if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target).Hash -eq (Get-FileHash -LiteralPath $source).Hash)
            {
                continue
            }

            $null = New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent)
            Copy-Item -LiteralPath $source -Destination $target -Force
        }
        catch
        {
            $failures++
            Write-Warning "Couldn't restore $target`: $($_.Exception.Message)"
        }
    }

    # Folders that the run created go too, unless they hold logs.
    if (Test-Path -LiteralPath $powerToysDataFolder)
    {
        $folders = @(Get-ChildItem -LiteralPath $powerToysDataFolder -Recurse -Directory -Force | Sort-Object { $_.FullName.Length } -Descending)
        foreach ($folder in $folders)
        {
            $relative = $folder.FullName.Substring($powerToysDataFolder.Length + 1)
            if (-not $snapshot.Folders.Contains($relative) -and $null -eq (Get-ChildItem -LiteralPath $folder.FullName -Force | Select-Object -First 1))
            {
                Remove-Item -LiteralPath $folder.FullName -Force -ErrorAction SilentlyContinue
            }
        }
    }

    if ($failures -eq 0)
    {
        Remove-Item -LiteralPath $snapshot.Path -Recurse -Force -ErrorAction SilentlyContinue
    }
    else
    {
        Write-Warning "Some PowerToys files couldn't be restored. The originals are in $($snapshot.Path)."
    }
}

function Get-RunnerLogOffsets
{
    $offsets = @{}
    foreach ($file in Get-ChildItem -Path $runnerLogFolder -Filter 'runner-log*.log' -ErrorAction SilentlyContinue)
    {
        $stream = [IO.File]::Open($file.FullName, 'Open', 'Read', 'ReadWrite, Delete')
        try { $offsets[$file.FullName] = $stream.Length } finally { $stream.Dispose() }
    }

    return $offsets
}

function Wait-RunnerStartupStages
{
    param([Parameter(Mandatory)][Diagnostics.Process]$Process, [hashtable]$Offsets, [int]$TimeoutMs = 60000)

    $pattern = '\[p-' + $Process.Id + '\].*Startup stages \(ms since process start\): (?<stages>.+?)\s*$'
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    do
    {
        foreach ($file in Get-ChildItem -Path $runnerLogFolder -Filter 'runner-log*.log' -ErrorAction SilentlyContinue)
        {
            $offset = 0
            if ($Offsets.ContainsKey($file.FullName))
            {
                $offset = $Offsets[$file.FullName]
            }

            $stream = [IO.File]::Open($file.FullName, 'Open', 'Read', 'ReadWrite, Delete')
            try
            {
                if ($stream.Length -le $offset)
                {
                    continue
                }

                $null = $stream.Seek($offset, 'Begin')
                $text = [IO.StreamReader]::new($stream).ReadToEnd()
            }
            finally
            {
                $stream.Dispose()
            }

            $match = [regex]::Match($text, $pattern, 'Multiline')
            if ($match.Success)
            {
                $stages = [ordered]@{}
                foreach ($pair in $match.Groups['stages'].Value -split ' ')
                {
                    $parts = $pair -split '='
                    if ($parts.Count -eq 2)
                    {
                        $stages[$parts[0] + 'Ms'] = [double]$parts[1]
                    }
                }

                return $stages
            }
        }

        if ($Process.HasExited)
        {
            throw "The runner exited with code $($Process.ExitCode) before it was ready."
        }

        Start-Sleep -Milliseconds 50
    }
    while ($stopwatch.ElapsedMilliseconds -lt $TimeoutMs)

    throw "The runner didn't log its startup stages within $TimeoutMs ms. The Runner scenario needs the runner log level at 'info' or more verbose."
}

function Start-TargetRunner
{
    param([switch]$WaitForStages)

    $offsets = Get-RunnerLogOffsets
    $process = Start-TargetProcess -Path $runnerPath
    $stages = $null
    if ($WaitForStages)
    {
        $stages = Wait-RunnerStartupStages -Process $process -Offsets $offsets
    }
    else
    {
        # Opening Settings only needs the tray window, which builds without stage logging have too.
        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        while ([PowerToysPerformance.WindowFinder]::FindTopLevelWindow($process.Id, 'PToyTrayIconWindow') -eq [IntPtr]::Zero)
        {
            if ($process.HasExited)
            {
                throw "The runner exited with code $($process.ExitCode) before it was ready."
            }

            if ($stopwatch.ElapsedMilliseconds -gt 60000)
            {
                throw "The runner didn't create its tray window within 60 s."
            }

            Start-Sleep -Milliseconds 50
        }

        try
        {
            $null = $process.WaitForInputIdle(60000)
        }
        catch
        {
        }
    }

    return [pscustomobject]@{ Process = $process; Stages = $stages }
}

function Stop-Runner
{
    param([Parameter(Mandatory)][Diagnostics.Process]$Process, [Parameter(Mandatory)][string]$Folder)

    # Same as Exit in the tray menu, so modules shut down normally.
    $trayWindow = [PowerToysPerformance.WindowFinder]::FindTopLevelWindow($Process.Id, 'PToyTrayIconWindow')
    if ($trayWindow -ne [IntPtr]::Zero)
    {
        $null = [PowerToysPerformance.WindowFinder]::PostClose($trayWindow)
    }

    if (-not $Process.WaitForExit(15000))
    {
        Stop-Processes -Processes @($Process) -TimeoutMs 5000
        if (-not $Process.HasExited)
        {
            throw "Couldn't stop PowerToys (pid $($Process.Id)). If it runs elevated, exit it or run this script elevated."
        }
    }

    # Module processes exit on their own once the runner is gone.
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    do
    {
        $remaining = Get-RootProcesses -Folder $Folder
        foreach ($moduleProcess in $remaining)
        {
            $moduleProcess.Dispose()
        }

        if ($remaining.Count -eq 0)
        {
            return
        }

        Start-Sleep -Milliseconds 200
    }
    while ($stopwatch.ElapsedMilliseconds -lt 15000)

    Stop-RootProcesses -Folder $Folder
}

function Stop-AllRunners
{
    foreach ($runner in @(Get-Process -Name 'PowerToys' -ErrorAction SilentlyContinue | Where-Object SessionId -eq $sessionId))
    {
        $path = $null
        try
        {
            $null = $runner.Handle
            $path = $runner.Path
        }
        catch
        {
        }

        if (-not $path)
        {
            if (Get-Process -Id $runner.Id -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq 'PowerToys' })
            {
                throw "Can't access the running PowerToys (pid $($runner.Id)). If it runs elevated, exit it or run this script elevated."
            }

            continue
        }

        Stop-Runner -Process $runner -Folder (Split-Path $path -Parent)
        if (-not $script:stoppedRunnerPaths.Contains($path))
        {
            $script:stoppedRunnerPaths.Add($path)
        }
    }
}

function Set-LastVersionRun
{
    $version = (Get-Item $runnerPath).VersionInfo
    $text = 'v{0}.{1}.{2}' -f $version.FileMajorPart, $version.FileMinorPart, $version.FileBuildPart
    if ($version.FilePrivatePart -ne 0)
    {
        $text += '.' + $version.FilePrivatePart
    }

    $null = New-Item -ItemType Directory -Force -Path $powerToysDataFolder
    [IO.File]::WriteAllText((Join-Path $powerToysDataFolder 'last_version_run.json'), '{"last_version":"' + $text + '"}')
}

function Measure-Runner
{
    for ($iteration = 1; $iteration -le $WarmupIterations + $Iterations; $iteration++)
    {
        $runner = Start-TargetRunner -WaitForStages
        Start-Sleep -Milliseconds $SettleMilliseconds

        $metrics = [ordered]@{}
        foreach ($key in $runner.Stages.Keys)
        {
            $metrics[$key] = $runner.Stages[$key]
        }

        $memory = Get-MemorySample -Process $runner.Process
        $metrics.WorkingSetMB = $memory.WorkingSetMB
        $metrics.PrivateMB = $memory.PrivateMB

        $all = Get-RootProcesses
        $metrics.ProcessCount = $all.Count
        $metrics.TotalWorkingSetMB = [Math]::Round((($all | ForEach-Object { $_.Refresh(); $_.WorkingSet64 } | Measure-Object -Sum).Sum) / 1MB, 1)
        foreach ($process in $all)
        {
            $process.Dispose()
        }

        Add-Sample -Name 'Runner' -Iteration $iteration -Metrics $metrics
        Stop-Runner -Process $runner.Process -Folder $root
    }
}

function Close-Settings
{
    param([Parameter(Mandatory)][Diagnostics.Process]$Process)

    $null = $Process.CloseMainWindow()
    if (-not $Process.WaitForExit(5000))
    {
        Stop-Processes -Processes @($Process)
    }
}

function Measure-Settings
{
    $runner = Start-TargetRunner
    try
    {
        # Let module processes finish starting before the first sample.
        Start-Sleep -Seconds 5

        for ($iteration = 1; $iteration -le $WarmupIterations + $Iterations; $iteration++)
        {
            foreach ($existing in Get-RootProcesses -Name 'PowerToys.Settings')
            {
                Close-Settings -Process $existing
            }

            Start-Sleep -Milliseconds 500
            $existingIds = @(Get-Process -Name 'PowerToys.Settings' -ErrorAction SilentlyContinue | ForEach-Object Id)
            $script:recorder.Clear()
            $start = [Diagnostics.Stopwatch]::GetTimestamp()
            $null = Start-TargetProcess -Path $runnerPath -Arguments '--open-settings=Dashboard'

            $settings = $null
            $stopwatch = [Diagnostics.Stopwatch]::StartNew()
            while ($null -eq $settings -and $stopwatch.ElapsedMilliseconds -lt 30000)
            {
                $settings = Get-RootProcesses -Name 'PowerToys.Settings' | Where-Object { $_.Id -notin $existingIds } | Select-Object -First 1
                if ($null -eq $settings)
                {
                    Start-Sleep -Milliseconds 10
                }
            }

            if ($null -eq $settings)
            {
                throw "PowerToys.Settings.exe didn't start within 30 s."
            }

            $settingsId = $settings.Id
            $shown = Wait-WindowShown -Description 'The Settings window' -Process $settings -Match {
                param($e) $e.ProcessId -eq $settingsId -and $e.Root -eq $e.Handle -and $e.ClassName -eq 'WinUIDesktopWin32WindowClass'
            }.GetNewClosure()
            $shellReady = Wait-AutomationElement -WindowHandle $shown.Handle -AutomationId 'DashboardNavItem'

            $metrics = [ordered]@{
                WindowShownMs = Get-ElapsedMs -From $start -To $shown.Timestamp
                ShellReadyMs = Get-ElapsedMs -From $start -To $shellReady
            }

            Start-Sleep -Milliseconds $SettleMilliseconds
            $memory = Get-MemorySample -Process $settings
            $metrics.WorkingSetMB = $memory.WorkingSetMB
            $metrics.PrivateMB = $memory.PrivateMB
            Add-Sample -Name 'Settings' -Iteration $iteration -Metrics $metrics

            Close-Settings -Process $settings
        }
    }
    finally
    {
        Stop-Runner -Process $runner.Process -Folder $root
    }
}

function Measure-PowerToysRun
{
    if (@(Get-Process -Name 'PowerToys.PowerLauncher' -ErrorAction SilentlyContinue | Where-Object SessionId -eq $sessionId).Count -gt 0)
    {
        throw 'PowerToys Run is already running, and its single-instance check would close the measured process. Close it first.'
    }

    for ($iteration = 1; $iteration -le $WarmupIterations + $Iterations; $iteration++)
    {
        $start = [Diagnostics.Stopwatch]::GetTimestamp()
        $process = Start-TargetProcess -Path $launcherPath
        if (-not $process.WaitForInputIdle(60000))
        {
            throw "PowerToys Run didn't become idle within 60 s."
        }

        $idle = [Diagnostics.Stopwatch]::GetTimestamp()
        if ($process.HasExited)
        {
            throw "PowerToys Run exited with code $($process.ExitCode) during startup."
        }

        $metrics = [ordered]@{ InputIdleMs = Get-ElapsedMs -From $start -To $idle }
        Start-Sleep -Milliseconds $SettleMilliseconds
        $memory = Get-MemorySample -Process $process
        $metrics.WorkingSetMB = $memory.WorkingSetMB
        $metrics.PrivateMB = $memory.PrivateMB
        Add-Sample -Name 'PowerToysRun' -Iteration $iteration -Metrics $metrics

        Stop-ProcessAndDescendants -Process $process
    }
}

function Measure-FileLocksmith
{
    # File Locksmith reads the paths to check from this file (UTF-16, one path per line, empty line at the end).
    $pathsFile = Join-Path $powerToysDataFolder 'File Locksmith\last-run.log'
    Backup-File -Path $pathsFile
    $null = New-Item -ItemType Directory -Force -Path (Split-Path $pathsFile -Parent)
    [IO.File]::WriteAllText($pathsFile, (Join-Path $workFolder 'sample.md') + "`n`n", [Text.UnicodeEncoding]::new($false, $false))

    for ($iteration = 1; $iteration -le $WarmupIterations + $Iterations; $iteration++)
    {
        $script:recorder.Clear()
        $start = [Diagnostics.Stopwatch]::GetTimestamp()
        $process = Start-TargetProcess -Path $fileLocksmithPath
        $processId = $process.Id
        $shown = Wait-WindowShown -Description 'The File Locksmith window' -Process $process -Match {
            param($e) $e.ProcessId -eq $processId -and $e.Root -eq $e.Handle -and $e.ClassName -eq 'WinUIDesktopWin32WindowClass'
        }.GetNewClosure()
        $ready = Wait-AutomationElement -WindowHandle $shown.Handle -AutomationId 'ReloadBtn'

        $metrics = [ordered]@{
            WindowShownMs = Get-ElapsedMs -From $start -To $shown.Timestamp
            UiReadyMs = Get-ElapsedMs -From $start -To $ready
        }

        Start-Sleep -Milliseconds $SettleMilliseconds
        $memory = Get-MemorySample -Process $process
        $metrics.WorkingSetMB = $memory.WorkingSetMB
        $metrics.PrivateMB = $memory.PrivateMB
        Add-Sample -Name 'FileLocksmith' -Iteration $iteration -Metrics $metrics

        Stop-ProcessAndDescendants -Process $process
    }
}

function Measure-PreviewHandler
{
    param([Parameter(Mandatory)][string]$Name)

    $handler = $previewHandlers[$Name]
    $hostWindow = [PowerToysPerformance.HostWindow]::new(1000, 750)
    try
    {
        $hostHandle = $hostWindow.Handle

        # Same command line as src\modules\previewpane\FileExplorerDllExporter: file, parent HWND, left, right, top, bottom.
        $arguments = '"{0}" {1:X} 0 {2} 0 {3}' -f (Join-Path $workFolder $handler.Sample), $hostHandle.ToInt64(), $hostWindow.ClientWidth, $hostWindow.ClientHeight

        for ($iteration = 1; $iteration -le $WarmupIterations + $Iterations; $iteration++)
        {
            $script:recorder.Clear()
            $start = [Diagnostics.Stopwatch]::GetTimestamp()
            $process = Start-TargetProcess -Path $handler.Path -Arguments $arguments
            $processId = $process.Id

            $shown = Wait-WindowShown -Description "The $Name window" -Process $process -Match {
                param($e) $e.ProcessId -eq $processId -and $e.Root -eq $hostHandle -and $e.Handle -ne $hostHandle
            }.GetNewClosure()
            $webView = Wait-WindowShown -Description "The $Name WebView2 window" -Process $process -Match {
                param($e) $e.Root -eq $hostHandle -and $e.ProcessId -ne $processId
            }.GetNewClosure()

            $metrics = [ordered]@{
                WindowShownMs = Get-ElapsedMs -From $start -To $shown.Timestamp
                WebViewShownMs = Get-ElapsedMs -From $start -To $webView.Timestamp
            }

            Start-Sleep -Milliseconds $SettleMilliseconds
            $memory = Get-MemorySample -Process $process
            $metrics.WorkingSetMB = $memory.WorkingSetMB
            $metrics.PrivateMB = $memory.PrivateMB
            Add-Sample -Name $Name -Iteration $iteration -Metrics $metrics

            Stop-ProcessAndDescendants -Process $process
        }
    }
    finally
    {
        $hostWindow.Dispose()
    }
}

function Measure-SvgThumbnail
{
    $svg = Join-Path $workFolder 'thumbnail.svg'
    $bitmap = Join-Path $workFolder 'thumbnail.bmp'

    for ($iteration = 1; $iteration -le $WarmupIterations + $Iterations; $iteration++)
    {
        Copy-Item -LiteralPath (Join-Path $workFolder 'sample.svg') -Destination $svg -Force
        Remove-Item -LiteralPath $bitmap -Force -ErrorAction SilentlyContinue

        # Same command line as src\modules\previewpane\FileExplorerDllExporter: file and thumbnail size.
        $start = [Diagnostics.Stopwatch]::GetTimestamp()
        $process = Start-TargetProcess -Path $svgThumbnailPath -Arguments ('"{0}" 256' -f $svg)
        $startTimeUtc = $process.StartTime.ToUniversalTime()
        if (-not $process.WaitForExit(60000))
        {
            Stop-ProcessAndDescendants -Process $process
            throw "The SVG thumbnail provider didn't exit within 60 s."
        }

        $exit = [Diagnostics.Stopwatch]::GetTimestamp()
        if (-not (Test-Path -LiteralPath $bitmap))
        {
            throw "The SVG thumbnail provider exited with code $($process.ExitCode) without writing $bitmap."
        }

        Add-Sample -Name 'SvgThumbnail' -Iteration $iteration -Metrics ([ordered]@{ ExitMs = Get-ElapsedMs -From $start -To $exit })
        $descendants = [PowerToysPerformance.ProcessTree]::GetDescendants($process.Id, $startTimeUtc)
        try
        {
            Wait-PinnedProcesses -Processes $descendants
        }
        finally
        {
            foreach ($descendant in $descendants)
            {
                $descendant.Dispose()
            }
        }
    }
}

function New-SampleFiles
{
    $null = New-Item -ItemType Directory -Force -Path $workFolder
    $markdown = @(
        '# PowerToys startup measurement',
        '',
        'A small document with a list, a table, and code, so the renderer does representative work.',
        '',
        '- one', '- two', '- three',
        '',
        '| Column | Value |', '|---|---|', '| a | 1 |', '| b | 2 |',
        '',
        '```csharp', 'Console.WriteLine("hello");', '```'
    ) -join "`n"
    [IO.File]::WriteAllText((Join-Path $workFolder 'sample.md'), $markdown)
    [IO.File]::WriteAllText((Join-Path $workFolder 'sample.json'), '{ "name": "PowerToys", "items": [1, 2, 3], "nested": { "enabled": true } }')
    [IO.File]::WriteAllText(
        (Join-Path $workFolder 'sample.svg'),
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><rect width="100" height="100" fill="#0078d4"/><circle cx="50" cy="50" r="30" fill="#ffffff"/></svg>')
}

function Get-Percentile
{
    param([double[]]$Values, [double]$Percentile)

    $sorted = @($Values | Sort-Object)
    $index = [Math]::Max(0, [Math]::Ceiling($Percentile * $sorted.Count) - 1)
    return [Math]::Round($sorted[$index], 1)
}

function Get-Summary
{
    $summary = [ordered]@{}
    foreach ($name in $Scenario)
    {
        $group = @($script:samples | Where-Object { $_.Scenario -eq $name -and -not $_.IsWarmup })
        if ($group.Count -eq 0)
        {
            continue
        }

        $metrics = [ordered]@{}
        $names = $group[0].PSObject.Properties.Name | Where-Object { $_ -notin 'Scenario', 'Iteration', 'IsWarmup' }
        foreach ($metric in $names)
        {
            $values = @($group | ForEach-Object { $_.$metric } | Where-Object { $null -ne $_ } | ForEach-Object { [double]$_ })
            if ($values.Count -eq 0)
            {
                continue
            }

            $metrics[$metric] = [ordered]@{
                Count = $values.Count
                Median = Get-Percentile -Values $values -Percentile 0.5
                P90 = Get-Percentile -Values $values -Percentile 0.9
                Min = [Math]::Round(($values | Measure-Object -Minimum).Minimum, 1)
                Max = [Math]::Round(($values | Measure-Object -Maximum).Maximum, 1)
            }
        }

        $summary[$name] = $metrics
    }

    return $summary
}

function Get-ModuleProfile
{
    $file = Join-Path $powerToysDataFolder 'settings.json'
    if (-not (Test-Path -LiteralPath $file))
    {
        return $null
    }

    try
    {
        $settings = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
        $modules = @($settings.enabled.PSObject.Properties)
        return [ordered]@{
            Enabled = @($modules | Where-Object { $_.Value -eq $true } | ForEach-Object Name | Sort-Object)
            Disabled = @($modules | Where-Object { $_.Value -eq $false } | ForEach-Object Name | Sort-Object)
        }
    }
    catch
    {
        return [ordered]@{ Error = $_.Exception.Message }
    }
}

if (-not (Test-Path -LiteralPath $runnerPath))
{
    throw "PowerToys.exe wasn't found in $root."
}

$required = @{
    Settings = $settingsPath
    PowerToysRun = $launcherPath
    FileLocksmith = $fileLocksmithPath
    SvgThumbnail = $svgThumbnailPath
}
foreach ($name in $previewHandlers.Keys)
{
    $required[$name] = $previewHandlers[$name].Path
}

foreach ($name in $Scenario)
{
    if ($required.ContainsKey($name) -and -not (Test-Path -LiteralPath $required[$name]))
    {
        throw "$name needs $($required[$name]), which doesn't exist."
    }
}

# Fail before stopping anything when the runner can't report its stages.
if ($Scenario -contains 'Runner' -and -not [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($runnerPath)).Contains('Startup stages (ms since process start)'))
{
    throw "The Runner scenario needs a runner that logs its startup stages, and $runnerPath doesn't. Measure a newer build, or leave out Runner."
}

if ($Scenario | Where-Object { $_ -in 'Runner', 'Settings', 'PowerToysRun' })
{
    # With "Always run as administrator" on, a non-elevated runner restarts itself elevated through UAC and exits,
    # and this script couldn't see or stop that elevated runner.
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    $generalSettings = Join-Path $powerToysDataFolder 'settings.json'
    if (-not $isAdmin -and (Test-Path -LiteralPath $generalSettings))
    {
        $runElevated = $false
        try
        {
            $runElevated = [bool](Get-Content -LiteralPath $generalSettings -Raw | ConvertFrom-Json).run_elevated
        }
        catch
        {
        }

        if ($runElevated)
        {
            throw "'Always run as administrator' is on, so the runner would restart itself elevated. Run this script elevated."
        }
    }
}

$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
New-SampleFiles
$started = Get-Date

try
{
    $script:recorder = [PowerToysPerformance.WindowShowRecorder]::new()

    if ($Scenario | Where-Object { $_ -in 'Runner', 'Settings', 'PowerToysRun' })
    {
        Stop-AllRunners
        $script:tookOver = $true
        Save-DataFolder
        Set-LastVersionRun
    }

    foreach ($name in $Scenario)
    {
        Write-Host "== $name"
        switch ($name)
        {
            'Runner' { Measure-Runner }
            'Settings' { Measure-Settings }
            'PowerToysRun' { Measure-PowerToysRun }
            'FileLocksmith' { Measure-FileLocksmith }
            'SvgThumbnail' { Measure-SvgThumbnail }
            default { Measure-PreviewHandler -Name $name }
        }
    }

    $result = [ordered]@{
        SchemaVersion = 1
        Label = $Label
        Timestamp = $started.ToString('o')
        PowerToysRoot = $root
        PowerToysVersion = (Get-Item $runnerPath).VersionInfo.FileVersion
        Machine = [ordered]@{
            OS = (Get-CimInstance Win32_OperatingSystem).Caption
            OSVersion = [Environment]::OSVersion.Version.ToString()
            Processor = (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name)
            LogicalProcessors = [Environment]::ProcessorCount
            Architecture = $env:PROCESSOR_ARCHITECTURE
            MemoryGB = [Math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 1)
        }
        ModuleProfile = Get-ModuleProfile
        Configuration = [ordered]@{
            Scenario = $Scenario
            Iterations = $Iterations
            WarmupIterations = $WarmupIterations
            SettleMilliseconds = $SettleMilliseconds
        }
        Summary = Get-Summary
        Samples = $script:samples
    }

    $resultPath = Join-Path $OutputDirectory ('{0}-{1}.json' -f $Label, $started.ToString('yyyyMMdd-HHmmss'))
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8

    Write-Host ''
    Write-Host '| Scenario | Metric | Median | P90 | Min | Max |'
    Write-Host '|---|---|--:|--:|--:|--:|'
    foreach ($scenarioName in $result.Summary.Keys)
    {
        foreach ($metric in $result.Summary[$scenarioName].Keys)
        {
            $s = $result.Summary[$scenarioName][$metric]
            Write-Host ('| {0} | {1} | {2} | {3} | {4} | {5} |' -f $scenarioName, $metric, $s.Median, $s.P90, $s.Min, $s.Max)
        }
    }

    Write-Host ''
    Write-Host "Results: $resultPath"
}
finally
{
    Stop-LaunchedProcesses
    if ($script:tookOver)
    {
        # Every runner was stopped first, so anything still running from this build was started by
        # the measured runs, for example module processes of a runner that crashed.
        Stop-RootProcesses
    }

    Restore-Files
    if ($null -ne $script:dataSnapshot)
    {
        Restore-DataFolder
    }

    foreach ($path in $script:stoppedRunnerPaths)
    {
        if (Test-Path -LiteralPath $path)
        {
            Start-Process -FilePath $path | Out-Null
        }
    }

    if ($null -ne $script:recorder)
    {
        $script:recorder.Dispose()
    }
}
