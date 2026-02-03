// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.Interop;
using static RunnerV2.NativeMethods;

namespace RunnerV2.Helpers
{
    internal static partial class TrayIconManager
    {
        private static bool _trayIconVisible;

        private static nint GetTrayIcon()
        {
            if (SettingsUtils.Default.GetSettings<GeneralSettings>().ShowThemeAdaptiveTrayIcon)
            {
                return Icon.ExtractAssociatedIcon(ThemeHelper.GetCurrentSystemTheme() ? "./Assets/PowerToysDark.ico" : "./Assets/PowerToysLight.ico")!.Handle;
            }
            else
            {
                return Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!.Handle;
            }
        }

        public static void UpdateTrayIcon()
        {
            if (!_trayIconVisible)
            {
                return;
            }

            NOTIFYICONDATA notifyicondata = GetNOTIFYICONDATA();
            Shell_NotifyIcon(0x1, ref notifyicondata);
        }

        private static NOTIFYICONDATA GetNOTIFYICONDATA() => new()
        {
            CbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            HWnd = Runner.RunnerHwnd,
            UId = 1,
            HIcon = GetTrayIcon(),
            UFlags = 0x0000001 | 0x00000002 | 0x4,
            UCallbackMessage = (uint)WindowMessages.ICON_NOTIFY,
            SzTip = "PowerToys v" + Assembly.GetExecutingAssembly().GetName().Version!.Major + "." + Assembly.GetExecutingAssembly().GetName().Version!.Minor + "." + Assembly.GetExecutingAssembly().GetName().Version!.Build,
        };

        internal static void StartTrayIcon()
        {
            if (_trayIconVisible)
            {
                return;
            }

            NOTIFYICONDATA notifyicondata = GetNOTIFYICONDATA();
            ChangeWindowMessageFilterEx(Runner.RunnerHwnd, 0x0111, 0x0001, IntPtr.Zero);

            Shell_NotifyIcon(NIMADD, ref notifyicondata);
            _trayIconVisible = true;
        }

        internal static void StopTrayIcon()
        {
            if (!_trayIconVisible)
            {
                return;
            }

            NOTIFYICONDATA notifyicondata = new()
            {
                CbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                HWnd = Runner.RunnerHwnd,
                UId = 1,
            };

            Shell_NotifyIcon(NIMDELETE, ref notifyicondata);
            _trayIconVisible = false;
        }

        internal enum TrayButton : uint
        {
            Settings = 1,
            Documentation,
            ReportBug,
            Close,
        }

        private static bool _doubleClickTimerRunning;
        private static bool _doubleClickDetected;

        private static IntPtr _trayIconMenu;

        static TrayIconManager()
        {
            _trayIconMenu = CreatePopupMenu();
            AppendMenuW(_trayIconMenu, 0u, new UIntPtr((uint)TrayButton.Settings), "Settings\tDouble-click");
            AppendMenuW(_trayIconMenu, 0x00000800u, UIntPtr.Zero, string.Empty); // separator
            AppendMenuW(_trayIconMenu, 0u, new UIntPtr((uint)TrayButton.Documentation), "Documentation");
            AppendMenuW(_trayIconMenu, 0u, new UIntPtr((uint)TrayButton.ReportBug), "Report a Bug");
            AppendMenuW(_trayIconMenu, 0x00000800u, UIntPtr.Zero, string.Empty); // separator
            AppendMenuW(_trayIconMenu, 0u, new UIntPtr((uint)TrayButton.Close), "Close");
            new ThemeListener().ThemeChanged += (_) =>
            {
                PostMessageW(Runner.RunnerHwnd, 0x0800, IntPtr.Zero, 0x9000);
            };
        }

        internal static void ProcessTrayIconMessage(long lParam)
        {
            switch (lParam)
            {
                case 0x0205: // WM_RBUTTONDBLCLK
                case 0x007B: // WM_CONTEXTMENU
                    SetForegroundWindow(Runner.RunnerHwnd);
                    TrackPopupMenu(_trayIconMenu, 0x0004 | 0x0020, Cursor.Position.X, Cursor.Position.Y, 0, Runner.RunnerHwnd, IntPtr.Zero);
                    break;
                case 0x0202: // WM_LBUTTONUP
                    if (_doubleClickTimerRunning)
                    {
                        break;
                    }

                    _doubleClickTimerRunning = true;
                    Task.Delay(SystemInformation.DoubleClickTime).ContinueWith(_ =>
                    {
                        if (!_doubleClickDetected)
                        {
                            if (SettingsUtils.Default.GetSettings<GeneralSettings>().EnableQuickAccess)
                            {
                                QuickAccessHelper.Show();
                            }
                            else
                            {
                                SettingsHelper.OpenSettingsWindow();
                            }
                        }

                        _doubleClickDetected = false;
                        _doubleClickTimerRunning = false;
                    });
                    break;
                case 0x0203: // WM_LBUTTONDBLCLK
                    _doubleClickDetected = true;
                    SettingsHelper.OpenSettingsWindow();
                    break;
                case 0x9000: // Update tray icon
                    UpdateTrayIcon();
                    break;
            }
        }

        internal static bool IsBugReportToolRunning { get; set; }

        internal static void ProcessTrayMenuCommand(nuint commandId)
        {
            switch ((TrayButton)commandId)
            {
                case TrayButton.Settings:
                    SettingsHelper.OpenSettingsWindow();
                    break;
                case TrayButton.Documentation:
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://aka.ms/PowerToysOverview",
                        UseShellExecute = true,
                    });
                    break;
                case TrayButton.ReportBug:
                    Logger.LogInfo("Starting bug report tool from tray menu");
                    Process bugReportProcess = new();
                    bugReportProcess.StartInfo = new ProcessStartInfo
                    {
                        FileName = "Tools\\PowerToys.BugReportTool.exe",
                        CreateNoWindow = true,
                    };

                    bugReportProcess.EnableRaisingEvents = true;

                    EnableMenuItem(_trayIconMenu, (uint)TrayButton.ReportBug, 0x000000 | 0x00001);

                    bugReportProcess.Exited += (sender, e) =>
                    {
                        bugReportProcess.Dispose();
                        EnableMenuItem(_trayIconMenu, (uint)TrayButton.ReportBug, 0x00000000);
                        IsBugReportToolRunning = false;
                        Logger.LogInfo("Bug report tool exited");
                    };

                    bugReportProcess.Start();
                    IsBugReportToolRunning = true;

                    break;
                case TrayButton.Close:
                    Runner.Close();
                    break;
            }
        }
    }
}
