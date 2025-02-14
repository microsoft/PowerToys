// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// <summary>
//     Screen/Desktop helper functions.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;

using Thread = MouseWithoutBorders.Core.Thread;

namespace MouseWithoutBorders
{
    // Desktops, and GetScreenConfig routines
    internal partial class Common
    {
        private static MyRectangle newDesktopBounds;
        private static MyRectangle newPrimaryScreenBounds;
        private static string activeDesktop;

        internal static string ActiveDesktop => Common.activeDesktop;

        private static void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            GetScreenConfig();
        }

        internal static readonly List<Point> SensitivePoints = new();

        private static bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
        {
            // lprcMonitor is wrong!!! => using GetMonitorInfo(...)
            // Log(String.Format( CultureInfo.CurrentCulture,"MONITOR: l{0}, t{1}, r{2}, b{3}", lprcMonitor.Left, lprcMonitor.Top, lprcMonitor.Right, lprcMonitor.Bottom));
            NativeMethods.MonitorInfoEx mi = default;
            mi.cbSize = Marshal.SizeOf(mi);
            _ = NativeMethods.GetMonitorInfo(hMonitor, ref mi);

            try
            {
                // For logging only
                _ = NativeMethods.GetDpiForMonitor(hMonitor, 0, out uint dpiX, out uint dpiY);
                Logger.Log(string.Format(CultureInfo.CurrentCulture, "MONITOR: ({0}, {1}, {2}, {3}). DPI: ({4}, {5})", mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom, dpiX, dpiY));
            }
            catch (DllNotFoundException)
            {
                Logger.Log("GetDpiForMonitor is unsupported in Windows 7 and lower.");
            }
            catch (EntryPointNotFoundException)
            {
                Logger.Log("GetDpiForMonitor is unsupported in Windows 7 and lower.");
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }

            if (mi.rcMonitor.Left == 0 && mi.rcMonitor.Top == 0 && mi.rcMonitor.Right != 0 && mi.rcMonitor.Bottom != 0)
            {
                // Primary screen
                _ = Interlocked.Exchange(ref screenWidth, mi.rcMonitor.Right - mi.rcMonitor.Left);
                _ = Interlocked.Exchange(ref screenHeight, mi.rcMonitor.Bottom - mi.rcMonitor.Top);

                newPrimaryScreenBounds.Left = mi.rcMonitor.Left;
                newPrimaryScreenBounds.Top = mi.rcMonitor.Top;
                newPrimaryScreenBounds.Right = mi.rcMonitor.Right;
                newPrimaryScreenBounds.Bottom = mi.rcMonitor.Bottom;
            }
            else
            {
                if (mi.rcMonitor.Left < newDesktopBounds.Left)
                {
                    newDesktopBounds.Left = mi.rcMonitor.Left;
                }

                if (mi.rcMonitor.Top < newDesktopBounds.Top)
                {
                    newDesktopBounds.Top = mi.rcMonitor.Top;
                }

                if (mi.rcMonitor.Right > newDesktopBounds.Right)
                {
                    newDesktopBounds.Right = mi.rcMonitor.Right;
                }

                if (mi.rcMonitor.Bottom > newDesktopBounds.Bottom)
                {
                    newDesktopBounds.Bottom = mi.rcMonitor.Bottom;
                }
            }

            lock (SensitivePoints)
            {
                SensitivePoints.Add(new Point(mi.rcMonitor.Left, mi.rcMonitor.Top));
                SensitivePoints.Add(new Point(mi.rcMonitor.Right, mi.rcMonitor.Top));
                SensitivePoints.Add(new Point(mi.rcMonitor.Right, mi.rcMonitor.Bottom));
                SensitivePoints.Add(new Point(mi.rcMonitor.Left, mi.rcMonitor.Bottom));
            }

            return true;
        }

        internal static void GetScreenConfig()
        {
            try
            {
                Logger.LogDebug("==================== GetScreenConfig started");
                newDesktopBounds = new MyRectangle();
                newPrimaryScreenBounds = new MyRectangle();
                newDesktopBounds.Left = newPrimaryScreenBounds.Left = Screen.PrimaryScreen.Bounds.Left;
                newDesktopBounds.Top = newPrimaryScreenBounds.Top = Screen.PrimaryScreen.Bounds.Top;
                newDesktopBounds.Right = newPrimaryScreenBounds.Right = Screen.PrimaryScreen.Bounds.Right;
                newDesktopBounds.Bottom = newPrimaryScreenBounds.Bottom = Screen.PrimaryScreen.Bounds.Bottom;

                Logger.Log(string.Format(
                    CultureInfo.CurrentCulture,
                    "logon = {0} PrimaryScreenBounds = {1},{2},{3},{4} desktopBounds = {5},{6},{7},{8}",
                    Common.RunOnLogonDesktop,
                    Common.newPrimaryScreenBounds.Left,
                    Common.newPrimaryScreenBounds.Top,
                    Common.newPrimaryScreenBounds.Right,
                    Common.newPrimaryScreenBounds.Bottom,
                    Common.newDesktopBounds.Left,
                    Common.newDesktopBounds.Top,
                    Common.newDesktopBounds.Right,
                    Common.newDesktopBounds.Bottom));

#if USE_MANAGED_ROUTINES
                // Managed routines do not work well when running on secure desktop:(
                screenWidth = Screen.PrimaryScreen.Bounds.Width;
                screenHeight = Screen.PrimaryScreen.Bounds.Height;
                screenCount = Screen.AllScreens.Length;
                for (int i = 0; i < Screen.AllScreens.Length; i++)
                {
                    if (Screen.AllScreens[i].Bounds.Left < desktopBounds.Left) desktopBounds.Left = Screen.AllScreens[i].Bounds.Left;
                    if (Screen.AllScreens[i].Bounds.Top < desktopBounds.Top) desktopBounds.Top = Screen.AllScreens[i].Bounds.Top;
                    if (Screen.AllScreens[i].Bounds.Right > desktopBounds.Right) desktopBounds.Right = Screen.AllScreens[i].Bounds.Right;
                    if (Screen.AllScreens[i].Bounds.Bottom > desktopBounds.Bottom) desktopBounds.Bottom = Screen.AllScreens[i].Bounds.Bottom;
                }
#else
                lock (SensitivePoints)
                {
                    SensitivePoints.Clear();
                }

                NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumProc, IntPtr.Zero);

                // 1000 calls to EnumDisplayMonitors cost a dozen of milliseconds
#endif
                Interlocked.Exchange(ref MachineStuff.desktopBounds, newDesktopBounds);
                Interlocked.Exchange(ref MachineStuff.primaryScreenBounds, newPrimaryScreenBounds);

                Logger.Log(string.Format(
                    CultureInfo.CurrentCulture,
                    "logon = {0} PrimaryScreenBounds = {1},{2},{3},{4} desktopBounds = {5},{6},{7},{8}",
                    Common.RunOnLogonDesktop,
                    MachineStuff.PrimaryScreenBounds.Left,
                    MachineStuff.PrimaryScreenBounds.Top,
                    MachineStuff.PrimaryScreenBounds.Right,
                    MachineStuff.PrimaryScreenBounds.Bottom,
                    MachineStuff.DesktopBounds.Left,
                    MachineStuff.DesktopBounds.Top,
                    MachineStuff.DesktopBounds.Right,
                    MachineStuff.DesktopBounds.Bottom));

                Logger.Log("==================== GetScreenConfig ended");
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

#if USING_SCREEN_SAVER_ROUTINES
                [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int PostMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr OpenDesktop(string hDesktop, int Flags, bool Inherit, UInt32 DesiredAccess);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDesktopWindows( IntPtr hDesktop, EnumDesktopWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(int uAction, int uParam, ref int pvParam, int flags);

        private delegate bool EnumDesktopWindowsProc(IntPtr hDesktop, IntPtr lParam);
        private const int WM_CLOSE = 16;
        private const int SPI_GETSCREENSAVERRUNNING = 114;

        internal static bool IsScreenSaverRunning()
        {
            int isRunning = 0;
            SystemParametersInfo(SPI_GETSCREENSAVERRUNNING, 0,ref isRunning, 0);
            return (isRunning != 0);
        }

        internal static void CloseScreenSaver()
        {
            IntPtr hDesktop = OpenDesktop("Screen-saver", 0, false, DESKTOP_READOBJECTS | DESKTOP_WRITEOBJECTS);
            if (hDesktop != IntPtr.Zero)
            {
                LogDebug("Closing screen saver...");
                EnumDesktopWindows(hDesktop, new EnumDesktopWindowsProc(CloseScreenSaverFunc), IntPtr.Zero);
                CloseDesktop(hDesktop);
            }
        }

        private static bool CloseScreenSaverFunc(IntPtr hWnd, IntPtr lParam)
        {
            if (IsWindowVisible(hWnd))
            {
                LogDebug("Posting WM_CLOSE to " + hWnd.ToString(CultureInfo.InvariantCulture));
                PostMessage(hWnd, WM_CLOSE, 0, 0);
            }
            return true;
        }
#endif

        internal static string GetMyDesktop()
        {
            byte[] arThreadDesktop = new byte[256];
            IntPtr hD = NativeMethods.GetThreadDesktop(NativeMethods.GetCurrentThreadId());
            if (hD != IntPtr.Zero)
            {
                _ = NativeMethods.GetUserObjectInformation(hD, NativeMethods.UOI_NAME, arThreadDesktop, arThreadDesktop.Length, out _);
                return GetString(arThreadDesktop).Replace("\0", string.Empty);
            }

            return string.Empty;
        }

        internal static string GetInputDesktop()
        {
            byte[] arInputDesktop = new byte[256];
            IntPtr hD = NativeMethods.OpenInputDesktop(0, false, NativeMethods.DESKTOP_READOBJECTS);
            if (hD != IntPtr.Zero)
            {
                _ = NativeMethods.GetUserObjectInformation(hD, NativeMethods.UOI_NAME, arInputDesktop, arInputDesktop.Length, out _);
                return GetString(arInputDesktop).Replace("\0", string.Empty);
            }

            return string.Empty;
        }

        internal static void StartMMService(string desktopToRunMouseWithoutBordersOn)
        {
            if (!Common.RunWithNoAdminRight)
            {
                Logger.LogDebug("*** Starting on active Desktop: " + desktopToRunMouseWithoutBordersOn);
                StartMouseWithoutBordersService(desktopToRunMouseWithoutBordersOn);
            }
        }

        internal static void CheckForDesktopSwitchEvent(bool cleanupIfExit)
        {
            try
            {
                if (!IsMyDesktopActive() || Common.CurrentProcess.SessionId != NativeMethods.WTSGetActiveConsoleSessionId())
                {
                    Common.RunDDHelper(true);
                    int waitCount = 20;

                    while (NativeMethods.WTSGetActiveConsoleSessionId() == 0xFFFFFFFF && waitCount > 0)
                    {
                        waitCount--;
                        Logger.LogDebug("The session is detached/attached.");
                        Thread.Sleep(500);
                    }

                    string myDesktop = GetMyDesktop();
                    activeDesktop = GetInputDesktop();

                    Logger.LogDebug("*** Active Desktop = " + activeDesktop);
                    Logger.LogDebug("*** My Desktop = " + myDesktop);

                    if (myDesktop.Equals(activeDesktop, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.LogDebug("*** Active Desktop == My Desktop (TS session)");
                    }

                    if (!activeDesktop.Equals("winlogon", StringComparison.OrdinalIgnoreCase) &&
                        !activeDesktop.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                        !activeDesktop.Equals("disconnect", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            StartMMService(activeDesktop);
                        }
                        catch (Exception e)
                        {
                            Logger.Log($"{nameof(CheckForDesktopSwitchEvent)}: {e}");
                        }
                    }
                    else
                    {
                        if (!myDesktop.Equals(activeDesktop, StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.Log("*** Active Desktop <> My Desktop");
                        }

                        uint sid = NativeMethods.WTSGetActiveConsoleSessionId();

                        if (Process.GetProcessesByName(Common.BinaryName).Any(p => (uint)p.SessionId == sid))
                        {
                            Logger.Log("Found MouseWithoutBorders on the active session!");
                        }
                        else
                        {
                            Logger.Log("MouseWithoutBorders not found on the active session!");
                            StartMMService(null);
                        }
                    }

                    if (!myDesktop.Equals("winlogon", StringComparison.OrdinalIgnoreCase) &&
                        !myDesktop.Equals("default", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.LogDebug("*** Desktop inactive, exiting: " + myDesktop);
                        Setting.Values.LastX = JUST_GOT_BACK_FROM_SCREEN_SAVER;
                        if (cleanupIfExit)
                        {
                            Common.Cleanup();
                        }

                        Process.GetCurrentProcess().KillProcess();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private static Point p;

        internal static bool IsMyDesktopActive()
        {
            return NativeMethods.GetCursorPos(ref p);
        }
    }
}
