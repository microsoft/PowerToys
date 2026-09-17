// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Also compiled by Windows PowerShell 5.1 in the disposable guest. Keep this file C# 5 compatible.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.MouseWithoutBorders.UITests
{
    public sealed class DesktopState
    {
        public DesktopState()
        {
            InputDesktop = string.Empty;
        }

        public int SessionId { get; set; }

        public int WtsState { get; set; }

        public string InputDesktop { get; set; }

        public bool InputAvailable { get; set; }

        public bool Elevated { get; set; }

        public bool IsSystem { get; set; }

        public long ForegroundHwnd { get; set; }

        public bool Ready
        {
            get { return SessionId > 0 && WtsState == 0 && InputDesktop == "Default" && InputAvailable && !IsSystem; }
        }
    }

    public sealed class ProcessIdentity
    {
        // Never add CommandLine: the Win10 Sandbox client embeds an AccountPassword there.
        public ProcessIdentity()
        {
            Path = string.Empty;
        }

        public int Id { get; set; }

        public int ParentId { get; set; }

        public int SessionId { get; set; }

        public string Path { get; set; }

        public DateTime StartTimeUtc { get; set; }

        public static ProcessIdentity Capture(int id)
        {
            using (Process process = Process.GetProcessById(id))
            {
                // Tag the failing stage: ERROR_GEN_FAILURE ("A device attached to the system is
                // not functioning") has been observed here without indicating whether the
                // toolhelp32 snapshot walk (ParentId), the process token/session query
                // (SessionId), or QueryFullProcessImageName (ImagePath) is the one that failed.
                int parentId;
                try { parentId = NativeSupport.ParentId(id); }
                catch (Exception error) { throw new InvalidOperationException("ParentId: " + error.Message, error); }
                int sessionId;
                try { sessionId = process.SessionId; }
                catch (Exception error) { throw new InvalidOperationException("SessionId: " + error.Message, error); }
                string path;
                try { path = NativeSupport.ImagePath(id); }
                catch (Exception error) { throw new InvalidOperationException("ImagePath: " + error.Message, error); }
                DateTime startTimeUtc;
                try { startTimeUtc = process.StartTime.ToUniversalTime(); }
                catch (Exception error) { throw new InvalidOperationException("StartTime: " + error.Message, error); }
                return new ProcessIdentity
                {
                    Id = id,
                    ParentId = parentId,
                    SessionId = sessionId,
                    Path = path,
                    StartTimeUtc = startTimeUtc,
                };
            }
        }

        public bool IsCurrent()
        {
            try
            {
                ProcessIdentity actual = Capture(Id);
                return actual.StartTimeUtc == StartTimeUtc.ToUniversalTime() && actual.SessionId == SessionId &&
                    actual.ParentId == ParentId && string.Equals(actual.Path, Path, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (Win32Exception)
            {
                try
                {
                    using (Process current = Process.GetProcessById(Id))
                    {
                        if (current.HasExited)
                        {
                            return false;
                        }
                    }
                }
                catch (ArgumentException)
                {
                    return false;
                }

                throw;
            }
        }

        public void Stop()
        {
            if (!IsCurrent())
            {
                return;
            }

            Process process;
            try
            {
                process = Process.GetProcessById(Id);
            }
            catch (ArgumentException)
            {
                return;
            }

            using (process)
            {
                try
                {
                    // Revalidate after opening the PID; never kill by executable name.
                    if (process.StartTime.ToUniversalTime() != StartTimeUtc.ToUniversalTime())
                    {
                        throw new InvalidOperationException("PID was reused during cleanup.");
                    }

                    process.Kill();
                    if (!process.WaitForExit(15000))
                    {
                        throw new TimeoutException("Owned process did not exit: " + Id);
                    }
                }
                catch (InvalidOperationException)
                {
                    if (!process.HasExited)
                    {
                        throw;
                    }
                }
                catch (Win32Exception)
                {
                    if (!process.HasExited)
                    {
                        throw;
                    }
                }
            }
        }
    }

    public sealed class WindowInfo
    {
        public WindowInfo()
        {
            Title = string.Empty;
        }

        public long Handle { get; set; }

        public string Title { get; set; }

        public bool Visible { get; set; }

        public bool HasOwner { get; set; }
    }

    public static class NativeSupport
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry
        {
            public uint Size;
            public uint Usage;
            public uint Id;
            public IntPtr DefaultHeap;
            public uint ModuleId;
            public uint Threads;
            public uint ParentId;
            public int Priority;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string Exe;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int id);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder name, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint id);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint currentThread, uint otherThread, bool attach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetLastActivePopup(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr dialog, int item);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder name, int count);

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint command);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);

        [DllImport("user32.dll")]
        private static extern bool CloseDesktop(IntPtr desktop);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder name, int length, out int needed);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQuerySessionInformation(IntPtr server, int session, int infoClass, out IntPtr buffer, out int bytes);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr buffer);

        public static int WindowProcessId(IntPtr window)
        {
            uint processId;
            GetWindowThreadProcessId(window, out processId);
            return (int)processId;
        }

        public static void FocusWindow(IntPtr window)
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == window)
            {
                return;
            }

            uint processId;
            uint foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out processId);
            uint currentThread = GetCurrentThreadId();
            bool attached = foregroundThread != 0 && foregroundThread != currentThread &&
                AttachThreadInput(currentThread, foregroundThread, true);
            try
            {
                BringWindowToTop(window);
                SetForegroundWindow(window);
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(currentThread, foregroundThread, false);
                }
            }
        }

        // Ground truth for whether a process has created any top-level window, independent of
        // .NET's Process.MainWindowHandle heuristic (first visible, unowned, titled window found
        // while walking each thread's own windows). Used to diagnose WinUI3 windows that may not
        // satisfy that heuristic even though a real, visible top-level HWND already exists.
        public static WindowInfo[] WindowsForProcess(int processId)
        {
            List<WindowInfo> windows = new List<WindowInfo>();
            EnumWindows(
                delegate(IntPtr window, IntPtr lParam)
                {
                    if (WindowProcessId(window) != processId)
                    {
                        return true;
                    }

                    int length = GetWindowTextLength(window);
                    StringBuilder title = new StringBuilder(length + 1);
                    if (length > 0)
                    {
                        GetWindowText(window, title, title.Capacity);
                    }

                    windows.Add(new WindowInfo
                    {
                        Handle = window.ToInt64(),
                        Title = title.ToString(),
                        Visible = IsWindowVisible(window),
                        HasOwner = GetWindow(window, 4 /* GW_OWNER */) != IntPtr.Zero,
                    });
                    return true;
                },
                IntPtr.Zero);
            return windows.ToArray();
        }

        public static string[] DialogStaticText(IntPtr dialog, int processId)
        {
            if (WindowProcessId(dialog) != processId)
            {
                throw new InvalidOperationException("Dialog ownership changed before reading its error text.");
            }

            var text = new List<string>();
            EnumChildWindows(
                dialog,
                delegate(IntPtr child, IntPtr unused)
                {
                    var className = new StringBuilder(64);
                    if (WindowProcessId(child) == processId &&
                        GetClassName(child, className, className.Capacity) > 0 &&
                        className.ToString() == "Static" && IsWindowVisible(child))
                    {
                        // GetWindowText reads another process's cached caption without
                        // sending WM_GETTEXT or invoking a UI Automation provider.
                        var caption = new StringBuilder(2048);
                        if (GetWindowText(child, caption, caption.Capacity) > 0)
                        {
                            text.Add(caption.ToString());
                        }
                    }

                    return text.Count < 16;
                },
                IntPtr.Zero);
            return text.ToArray();
        }

        public static void ConfirmSandboxClose(IntPtr viewer, int processId)
        {
            IntPtr dialog = GetLastActivePopup(viewer);
            StringBuilder name = new StringBuilder(256);
            GetClassName(dialog, name, name.Capacity);
            if (WindowProcessId(dialog) != processId || name.ToString() != "#32770")
            {
                return;
            }

            IntPtr yes = GetDlgItem(dialog, 6);
            if (yes != IntPtr.Zero && WindowProcessId(yes) == processId)
            {
                PostMessage(yes, 0x00F5, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public static string ImagePath(int id)
        {
            Exception primaryFailure;
            using (SafeProcessHandle handle = OpenProcess(0x1000, false, id))
            {
                StringBuilder name = new StringBuilder(32768);
                int size = name.Capacity;
                if (!handle.IsInvalid && QueryFullProcessImageName(handle, 0, name, ref size))
                {
                    return name.ToString();
                }

                primaryFailure = new Win32Exception(Marshal.GetLastWin32Error());
            }

            // QueryFullProcessImageName has been observed to fail with ERROR_GEN_FAILURE ("A
            // device attached to the system is not functioning") for a live, non-exiting
            // WinUI3 process under this nested Sandbox, while the process is otherwise
            // enumerable. Process.MainModule.FileName walks a different internal code path
            // (PROCESS_QUERY_INFORMATION | PROCESS_VM_READ); try it before giving up.
            try
            {
                using (Process process = Process.GetProcessById(id))
                {
                    return process.MainModule.FileName;
                }
            }
            catch (Exception fallbackFailure)
            {
                throw new AggregateException(primaryFailure, fallbackFailure);
            }
        }

        public static int ParentId(int id)
        {
            IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
            if (snapshot == new IntPtr(-1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                ProcessEntry entry = new ProcessEntry { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry)) };
                if (Process32First(snapshot, ref entry))
                {
                    do
                    {
                        if (entry.Id == id)
                        {
                            return (int)entry.ParentId;
                        }
                    }
                    while (Process32Next(snapshot, ref entry));
                }

                throw new InvalidOperationException("Process exited before parent identity could be read.");
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }

        public static DesktopState Desktop()
        {
            int session;
            using (Process current = Process.GetCurrentProcess())
            {
                session = current.SessionId;
            }

            IntPtr buffer;
            int bytes;
            if (!WTSQuerySessionInformation(IntPtr.Zero, session, 8, out buffer, out bytes))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            int state;
            try
            {
                state = Marshal.ReadInt32(buffer);
            }
            finally
            {
                WTSFreeMemory(buffer);
            }

            StringBuilder name = new StringBuilder(256);
            IntPtr desktop = OpenInputDesktop(0, false, 1);
            if (desktop != IntPtr.Zero)
            {
                try
                {
                    if (!GetUserObjectInformation(desktop, 2, name, 512, out bytes))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                finally
                {
                    CloseDesktop(desktop);
                }
            }

            Point point;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                return new DesktopState
                {
                    SessionId = session,
                    WtsState = state,
                    InputDesktop = name.ToString(),
                    InputAvailable = GetCursorPos(out point),
                    IsSystem = identity.IsSystem,
                    Elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator),
                    ForegroundHwnd = GetForegroundWindow().ToInt64(),
                };
            }
        }
    }
}
