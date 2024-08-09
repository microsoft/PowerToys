// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Awake.Core.Models;
using Awake.Core.Native;
using Awake.Properties;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Awake.Core
{
    /// <summary>
    /// Helper class used to manage the system tray.
    /// </summary>
    /// <remarks>
    /// Because Awake is a console application, there is no built-in
    /// way to embed UI components so we have to heavily rely on the native Windows API.
    /// </remarks>
    internal static class TrayHelper
    {
        private static NotifyIconData _notifyIconData;

        private static IntPtr _trayMenu;

        private static IntPtr TrayMenu { get => _trayMenu; set => _trayMenu = value; }

        private static IntPtr _hiddenWindowHandle;

        internal static IntPtr HiddenWindowHandle { get => _hiddenWindowHandle; private set => _hiddenWindowHandle = value; }

        static TrayHelper()
        {
            TrayMenu = IntPtr.Zero;
            HiddenWindowHandle = IntPtr.Zero;
        }

        public static void InitializeTray(string text, Icon icon)
        {
            CreateHiddenWindow(icon, text);
        }

        private static void ShowContextMenu(IntPtr hWnd)
        {
            if (TrayMenu != IntPtr.Zero)
            {
                Bridge.SetForegroundWindow(hWnd);

                // Get the handle to the context menu associated with the tray icon
                IntPtr hMenu = TrayMenu;

                // Get the current cursor position
                Bridge.GetCursorPos(out Models.Point cursorPos);

                Bridge.ScreenToClient(hWnd, ref cursorPos);

                MenuInfo menuInfo = new()
                {
                    CbSize = (uint)Marshal.SizeOf(typeof(MenuInfo)),
                    FMask = Native.Constants.MIM_STYLE,
                    DwStyle = Native.Constants.MNS_AUTO_DISMISS,
                };
                Bridge.SetMenuInfo(hMenu, ref menuInfo);

                // Display the context menu at the cursor position
                Bridge.TrackPopupMenuEx(
                      hMenu,
                      Native.Constants.TPM_LEFT_ALIGN | Native.Constants.TPM_BOTTOMALIGN | Native.Constants.TPM_LEFT_BUTTON,
                      cursorPos.X,
                      cursorPos.Y,
                      hWnd,
                      IntPtr.Zero);
            }
            else
            {
                // Tray menu was not initialized. Log the issue.
                // This is normal when operating in "standalone mode" - that is, detached
                // from the PowerToys configuration file.
                Logger.LogError("Tried to create a context menu while the TrayMenu object is a null pointer. Normal when used in standalone mode.");
            }
        }

        private static void CreateHiddenWindow(Icon icon, string text)
        {
            IntPtr hWnd = IntPtr.Zero;

            // Start the message loop asynchronously
            Task.Run(() =>
            {
                RunOnMainThread(() =>
                {
                    WndClassEx wcex = new()
                    {
                        CbSize = (uint)Marshal.SizeOf(typeof(WndClassEx)),
                        Style = 0,
                        LpfnWndProc = Marshal.GetFunctionPointerForDelegate<Bridge.WndProcDelegate>(WndProc),
                        CbClsExtra = 0,
                        CbWndExtra = 0,
                        HInstance = Marshal.GetHINSTANCE(typeof(Program).Module),
                        HIcon = IntPtr.Zero,
                        HCursor = IntPtr.Zero,
                        HbrBackground = IntPtr.Zero,
                        LpszMenuName = string.Empty,
                        LpszClassName = Constants.TrayWindowId,
                        HIconSm = IntPtr.Zero,
                    };

                    Bridge.RegisterClassEx(ref wcex);

                    hWnd = Bridge.CreateWindowEx(
                        0,
                        Constants.TrayWindowId,
                        text,
                        0x00CF0000 | 0x00000001 | 0x00000008, // WS_OVERLAPPEDWINDOW | WS_VISIBLE | WS_MINIMIZEBOX
                        0,
                        0,
                        0,
                        0,
                        unchecked(-3),
                        IntPtr.Zero,
                        Marshal.GetHINSTANCE(typeof(Program).Module),
                        IntPtr.Zero);

                    if (hWnd == IntPtr.Zero)
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        throw new Win32Exception(errorCode, "Failed to add tray icon. Error code: " + errorCode);
                    }

                    // Keep this as a reference because we will need it when we update
                    // the tray icon in the future.
                    HiddenWindowHandle = hWnd;

                    Bridge.ShowWindow(hWnd, 0); // SW_HIDE
                    Bridge.UpdateWindow(hWnd);

                    SetShellIcon(hWnd, text, icon);

                    RunMessageLoop();
                });
            });
        }

        internal static void SetShellIcon(IntPtr hWnd, string text, Icon? icon, TrayIconAction action = TrayIconAction.Add)
        {
            int message = Native.Constants.NIM_ADD;

            switch (action)
            {
                case TrayIconAction.Update:
                    message = Native.Constants.NIM_MODIFY;
                    break;
                case TrayIconAction.Delete:
                    message = Native.Constants.NIM_DELETE;
                    break;
                case TrayIconAction.Add:
                default:
                    break;
            }

            if (action == TrayIconAction.Add || action == TrayIconAction.Update)
            {
                _notifyIconData = new NotifyIconData
                {
                    CbSize = Marshal.SizeOf(typeof(NotifyIconData)),
                    HWnd = hWnd,
                    UId = 1000,
                    UFlags = Native.Constants.NIF_ICON | Native.Constants.NIF_TIP | Native.Constants.NIF_MESSAGE,
                    UCallbackMessage = (int)Native.Constants.WM_USER,
                    HIcon = icon?.Handle ?? IntPtr.Zero,
                    SzTip = text,
                };
            }
            else if (action == TrayIconAction.Delete)
            {
                _notifyIconData = new NotifyIconData
                {
                    CbSize = Marshal.SizeOf(typeof(NotifyIconData)),
                    HWnd = hWnd,
                    UId = 1000,
                    UFlags = 0,
                };
            }

            if (!Bridge.Shell_NotifyIcon(message, ref _notifyIconData))
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, $"Failed to change tray icon. Action: {action} and error code: {errorCode}");
            }

            if (action == TrayIconAction.Delete)
            {
                _notifyIconData = default;
            }
        }

        private static void RunMessageLoop()
        {
            while (Bridge.GetMessage(out Msg msg, IntPtr.Zero, 0, 0))
            {
                Bridge.TranslateMessage(ref msg);
                Bridge.DispatchMessage(ref msg);
            }
        }

        private static int WndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            switch (message)
            {
                case Native.Constants.WM_USER:
                    if (lParam == (IntPtr)Native.Constants.WM_LBUTTONDOWN || lParam == (IntPtr)Native.Constants.WM_RBUTTONDOWN)
                    {
                        // Show the context menu associated with the tray icon
                        ShowContextMenu(hWnd);
                    }

                    break;
                case Native.Constants.WM_DESTROY:
                    // Clean up resources when the window is destroyed
                    Bridge.PostQuitMessage(0);
                    break;
                case Native.Constants.WM_COMMAND:
                    int trayCommandsSize = Enum.GetNames(typeof(TrayCommands)).Length;

                    long targetCommandIndex = wParam.ToInt64() & 0xFFFF;

                    switch (targetCommandIndex)
                    {
                        case (uint)TrayCommands.TC_EXIT:
                            {
                                Manager.CompleteExit(Environment.ExitCode);
                                break;
                            }

                        case (uint)TrayCommands.TC_DISPLAY_SETTING:
                            {
                                Manager.SetDisplay();
                                break;
                            }

                        case (uint)TrayCommands.TC_MODE_INDEFINITE:
                            {
                                AwakeSettings settings = Manager.ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName);
                                Manager.SetIndefiniteKeepAwake(keepDisplayOn: settings.Properties.KeepDisplayOn);
                                break;
                            }

                        case (uint)TrayCommands.TC_MODE_PASSIVE:
                            {
                                Manager.SetPassiveKeepAwake();
                                break;
                            }

                        default:
                            {
                                if (targetCommandIndex >= trayCommandsSize)
                                {
                                    AwakeSettings settings = Manager.ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName);
                                    if (settings.Properties.CustomTrayTimes.Count == 0)
                                    {
                                        settings.Properties.CustomTrayTimes.AddRange(Manager.GetDefaultTrayOptions());
                                    }

                                    int index = (int)targetCommandIndex - (int)TrayCommands.TC_TIME;
                                    uint targetTime = (uint)settings.Properties.CustomTrayTimes.ElementAt(index).Value;
                                    Manager.SetTimedKeepAwake(targetTime, keepDisplayOn: settings.Properties.KeepDisplayOn);
                                }

                                break;
                            }
                    }

                    break;
                default:
                    // Let the default window procedure handle other messages
                    return Bridge.DefWindowProc(hWnd, message, wParam, lParam);
            }

            return Bridge.DefWindowProc(hWnd, message, wParam, lParam);
        }

        internal static void RunOnMainThread(Action action)
        {
            var syncContext = new SingleThreadSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(syncContext);

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            syncContext.Post(
            _ =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: " + e.Message);
                }
                finally
                {
                    syncContext.EndMessageLoop();
                }
            },
            null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

            syncContext.BeginMessageLoop();
        }

        internal static void SetTray(AwakeSettings settings, bool startedFromPowerToys)
        {
            SetTray(
                settings.Properties.KeepDisplayOn,
                settings.Properties.Mode,
                settings.Properties.CustomTrayTimes,
                startedFromPowerToys);
        }

        public static void SetTray(bool keepDisplayOn, AwakeMode mode, Dictionary<string, int> trayTimeShortcuts, bool startedFromPowerToys)
        {
            ClearExistingTrayMenu();
            CreateNewTrayMenu(startedFromPowerToys, keepDisplayOn, mode);

            InsertAwakeModeMenuItems(mode);

            EnsureDefaultTrayTimeShortcuts(trayTimeShortcuts);
            CreateAwakeTimeSubMenu(trayTimeShortcuts, mode == AwakeMode.TIMED);
        }

        private static void ClearExistingTrayMenu()
        {
            if (TrayMenu != IntPtr.Zero && !Bridge.DestroyMenu(TrayMenu))
            {
                int errorCode = Marshal.GetLastWin32Error();
                Logger.LogError($"Failed to destroy menu: {errorCode}");
            }
        }

        private static void CreateNewTrayMenu(bool startedFromPowerToys, bool keepDisplayOn, AwakeMode mode)
        {
            TrayMenu = Bridge.CreatePopupMenu();
            if (TrayMenu == IntPtr.Zero)
            {
                return;
            }

            if (!startedFromPowerToys)
            {
                InsertMenuItem(0, TrayCommands.TC_EXIT, Resources.AWAKE_EXIT);
            }

            InsertMenuItem(0, TrayCommands.TC_DISPLAY_SETTING, Resources.AWAKE_KEEP_SCREEN_ON, keepDisplayOn, mode == AwakeMode.PASSIVE);

            if (!startedFromPowerToys)
            {
                InsertSeparator(1);
            }
        }

        private static void InsertMenuItem(int position, TrayCommands command, string text, bool checkedState = false, bool disabled = false)
        {
            uint state = Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING;
            state |= checkedState ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED;
            state |= disabled ? Native.Constants.MF_DISABLED : Native.Constants.MF_ENABLED;

            Bridge.InsertMenu(TrayMenu, (uint)position, state, (uint)command, text);
        }

        private static void InsertSeparator(int position)
        {
            Bridge.InsertMenu(TrayMenu, (uint)position, Native.Constants.MF_BYPOSITION | Native.Constants.MF_SEPARATOR, 0, string.Empty);
        }

        private static void EnsureDefaultTrayTimeShortcuts(Dictionary<string, int> trayTimeShortcuts)
        {
            if (trayTimeShortcuts.Count == 0)
            {
                trayTimeShortcuts.AddRange(Manager.GetDefaultTrayOptions());
            }
        }

        private static void CreateAwakeTimeSubMenu(Dictionary<string, int> trayTimeShortcuts, bool isChecked = false)
        {
            var awakeTimeMenu = Bridge.CreatePopupMenu();
            for (int i = 0; i < trayTimeShortcuts.Count; i++)
            {
                Bridge.InsertMenu(awakeTimeMenu, (uint)i, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING, (uint)TrayCommands.TC_TIME + (uint)i, trayTimeShortcuts.ElementAt(i).Key);
            }

            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_POPUP | (isChecked == true ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)awakeTimeMenu, Resources.AWAKE_KEEP_ON_INTERVAL);
        }

        private static void InsertAwakeModeMenuItems(AwakeMode mode)
        {
            InsertSeparator(0);

            InsertMenuItem(0, TrayCommands.TC_MODE_PASSIVE, Resources.AWAKE_OFF, mode == AwakeMode.PASSIVE);
            InsertMenuItem(0, TrayCommands.TC_MODE_INDEFINITE, Resources.AWAKE_KEEP_INDEFINITELY, mode == AwakeMode.INDEFINITE);
            InsertMenuItem(0, TrayCommands.TC_MODE_EXPIRABLE, Resources.AWAKE_KEEP_UNTIL_EXPIRATION, mode == AwakeMode.EXPIRABLE, true);
        }
    }
}
