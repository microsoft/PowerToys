// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
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
        private static ManualResetEvent? _exitSignal;
        private static IntPtr _hookID = IntPtr.Zero;

        private static IntPtr _trayMenu;

        private static IntPtr TrayMenu { get => _trayMenu; set => _trayMenu = value; }

        static TrayHelper()
        {
            TrayMenu = IntPtr.Zero;
        }

        public static void InitializeTray(string text, Icon icon, ManualResetEvent? exitSignal)
        {
            _exitSignal = exitSignal;

            CreateHiddenWindow(icon, text);
        }

        private static void ShowContextMenu(IntPtr hWnd)
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
                DwStyle = Native.Constants.MNS_AUTODISMISS | Native.Constants.MNS_NOTIFYBYPOS,
            };
            Bridge.SetMenuInfo(hMenu, ref menuInfo);

            // Display the context menu at the cursor position
            Bridge.TrackPopupMenuEx(
                  hMenu,
                  Native.Constants.TPM_LEFTALIGN | Native.Constants.TPM_BOTTOMALIGN | Native.Constants.TPM_LEFTBUTTON,
                  cursorPos.X,
                  cursorPos.Y,
                  hWnd,
                  IntPtr.Zero);
        }

        private static void CreateHiddenWindow(Icon icon, string text)
        {
            IntPtr hWnd = IntPtr.Zero;

            // Start the message loop asynchronously
            Task.Run(() =>
            {
                RunOnMainThread(() =>
                {
                    // Register window class
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
                        LpszClassName = "Awake.MessageWindow",
                        HIconSm = IntPtr.Zero,
                    };

                    Bridge.RegisterClassEx(ref wcex);

                    // Create window
                    hWnd = Bridge.CreateWindowEx(
                        0,
                        "Awake.MessageWindow",
                        "PowerToys Awake",
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

                        // return; // Exit the method if window creation fails
                    }

                    _notifyIconData = new NotifyIconData
                    {
                        CbSize = Marshal.SizeOf(typeof(NotifyIconData)),
                        HWnd = hWnd,
                        UId = 1000,
                        UFlags = Native.Constants.NIF_ICON | Native.Constants.NIF_TIP | Native.Constants.NIF_MESSAGE,
                        UCallbackMessage = (int)Native.Constants.WM_USER,
                        HIcon = icon.Handle,
                        SzTip = text,
                    };

                    // Show and update window
                    Bridge.ShowWindow(hWnd, 0); // SW_HIDE
                    Bridge.UpdateWindow(hWnd);

                    if (!Bridge.Shell_NotifyIcon(Native.Constants.NIM_ADD, ref _notifyIconData))
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        throw new Win32Exception(errorCode, "Failed to add tray icon. Error code: " + errorCode);
                    }

                    // Run the message loop
                    RunMessageLoop();
                });
            });
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
            Logger.LogInfo($"{message.ToString(CultureInfo.InvariantCulture)} W: {wParam} L: {lParam}");

            switch (message)
            {
                case Native.Constants.WM_USER:
                    if (lParam == (IntPtr)Native.Constants.WM_LBUTTONDOWN || lParam == (IntPtr)Native.Constants.WM_RBUTTONDOWN)
                    {
                        // Show the context menu associated with the tray icon
                        ShowContextMenu(hWnd);
                    }

                    break;
                case (uint)TrayCommands.TC_EXIT:
                    Manager.CompleteExit(0, _exitSignal, true);
                    break;
                case (uint)TrayCommands.TC_DISPLAY_SETTING:
                    Manager.SetDisplay();
                    break;
                case (uint)TrayCommands.TC_MODE_INDEFINITE:
                    Manager.SetIndefiniteKeepAwake();
                    break;
                case (uint)TrayCommands.TC_MODE_PASSIVE:
                    Manager.SetPassiveKeepAwake();
                    break;
                case Native.Constants.WM_DESTROY:
                    // Clean up resources when the window is destroyed
                    Bridge.PostQuitMessage(0);
                    break;
                case Native.Constants.WM_COMMAND:
                    int trayCommandsSize = Enum.GetNames(typeof(TrayCommands)).Length;

                    if (message == (int)Native.Constants.WM_COMMAND)
                    {
                        long targetCommandIndex = wParam.ToInt64() & 0xFFFF;

                        switch (targetCommandIndex)
                        {
                            default:
                                if (targetCommandIndex >= trayCommandsSize)
                                {
                                    AwakeSettings settings = Manager.ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName);
                                    if (settings.Properties.CustomTrayTimes.Count == 0)
                                    {
                                        settings.Properties.CustomTrayTimes.AddRange(Manager.GetDefaultTrayOptions());
                                    }

                                    int index = (int)targetCommandIndex - (int)TrayCommands.TC_TIME;
                                    uint targetTime = (uint)settings.Properties.CustomTrayTimes.ElementAt(index).Value;
                                    Manager.SetTimedKeepAwake(targetTime);
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

        internal static void SetTray(string text, AwakeSettings settings, bool startedFromPowerToys)
        {
            SetTray(
                text,
                settings.Properties.KeepDisplayOn,
                settings.Properties.Mode,
                settings.Properties.CustomTrayTimes,
                startedFromPowerToys);
        }

        public static void SetTray(string text, bool keepDisplayOn, AwakeMode mode, Dictionary<string, int> trayTimeShortcuts, bool startedFromPowerToys)
        {
            // Clear the existing tray menu
            if (TrayMenu != IntPtr.Zero && Bridge.DestroyMenu(TrayMenu))
            {
                Logger.LogError("Failed to destroy menu.");
            }

            // Create a new tray menu
            TrayMenu = Bridge.CreatePopupMenu();

            if (TrayMenu != IntPtr.Zero)
            {
                if (!startedFromPowerToys)
                {
                    // Insert menu items for exiting Awake if not started from PowerToys
                    Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING, (uint)TrayCommands.TC_EXIT, Resources.AWAKE_EXIT);
                    Bridge.InsertMenu(TrayMenu, 1, Native.Constants.MF_BYPOSITION | Native.Constants.MF_SEPARATOR, 0, string.Empty);
                }

                // Insert menu item for toggling display setting
                Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING | (keepDisplayOn ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED) | (mode == AwakeMode.PASSIVE ? Native.Constants.MF_DISABLED : Native.Constants.MF_ENABLED), (uint)TrayCommands.TC_DISPLAY_SETTING, Resources.AWAKE_KEEP_SCREEN_ON);
            }

            // Ensure there are default tray time shortcuts
            if (trayTimeShortcuts.Count == 0)
            {
                trayTimeShortcuts.AddRange(Manager.GetDefaultTrayOptions());
            }

            // Create a submenu for awake time options
            var awakeTimeMenu = Bridge.CreatePopupMenu();
            for (int i = 0; i < trayTimeShortcuts.Count; i++)
            {
                Bridge.InsertMenu(awakeTimeMenu, (uint)i, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING, (uint)TrayCommands.TC_TIME + (uint)i, trayTimeShortcuts.ElementAt(i).Key);
            }

            // Insert menu items for different awake modes
            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_SEPARATOR, 0, string.Empty);
            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING | (mode == AwakeMode.PASSIVE ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_PASSIVE, Resources.AWAKE_OFF);
            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING | (mode == AwakeMode.INDEFINITE ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_INDEFINITE, Resources.AWAKE_KEEP_INDEFINITELY);
            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_POPUP | (mode == AwakeMode.TIMED ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)awakeTimeMenu, Resources.AWAKE_KEEP_ON_INTERVAL);
            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING | Native.Constants.MF_DISABLED | (mode == AwakeMode.EXPIRABLE ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_EXPIRABLE, Resources.AWAKE_KEEP_UNTIL_EXPIRATION);
        }
    }
}
