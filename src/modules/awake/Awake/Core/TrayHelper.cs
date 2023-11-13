// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
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
        private static IntPtr _trayMenu;

        private static IntPtr TrayMenu { get => _trayMenu; set => _trayMenu = value; }

        private static NotifyIcon TrayIcon { get; set; }

        static TrayHelper()
        {
            TrayIcon = new NotifyIcon();
        }

        public static void InitializeTray(string text, Icon icon, ManualResetEvent? exitSignal, ContextMenuStrip? contextMenu = null)
        {
            Task.Factory.StartNew(
                (tray) =>
                {
                    try
                    {
                        Logger.LogInfo("Setting up the tray.");
                        if (tray != null)
                        {
                            ((NotifyIcon)tray).Text = text;
                            ((NotifyIcon)tray).Icon = icon;
                            ((NotifyIcon)tray).ContextMenuStrip = contextMenu;
                            ((NotifyIcon)tray).Visible = true;
                            ((NotifyIcon)tray).MouseClick += TrayClickHandler;
                            Application.AddMessageFilter(new TrayMessageFilter(exitSignal));
                            Application.Run();
                            Logger.LogInfo("Tray setup complete.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"An error occurred initializing the tray. {ex.Message}");
                        Logger.LogError($"{ex.StackTrace}");
                    }
                },
                TrayIcon);
        }

        /// <summary>
        /// Function used to construct the context menu in the tray natively.
        /// </summary>
        /// <remarks>
        /// We need to use the Windows API here instead of the common control exposed
        /// by NotifyIcon because the one that is built into the Windows Forms stack
        /// hasn't been updated in a while and is looking like Office XP. That introduces
        /// scalability and coloring changes on any OS past Windows XP.
        /// </remarks>
        /// <param name="sender">The sender that triggers the handler.</param>
        /// <param name="e">MouseEventArgs instance containing mouse click event information.</param>
        private static void TrayClickHandler(object? sender, MouseEventArgs e)
        {
            IntPtr windowHandle = Manager.GetHiddenWindow();

            if (windowHandle != IntPtr.Zero)
            {
                Bridge.SetForegroundWindow(windowHandle);
                Bridge.TrackPopupMenuEx(TrayMenu, 0, Cursor.Position.X, Cursor.Position.Y, windowHandle, IntPtr.Zero);
            }
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
            if (TrayMenu != IntPtr.Zero)
            {
                var destructionStatus = Bridge.DestroyMenu(TrayMenu);
                if (destructionStatus != true)
                {
                    Logger.LogError("Failed to destroy menu.");
                }
            }

            TrayMenu = Bridge.CreatePopupMenu();

            if (TrayMenu != IntPtr.Zero)
            {
                if (!startedFromPowerToys)
                {
                    // If Awake is started from PowerToys, the correct way to exit it is disabling it from Settings.
                    Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING, (uint)TrayCommands.TC_EXIT, Resources.AWAKE_EXIT);
                    Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_SEPARATOR, 0, string.Empty);
                }

                Bridge.InsertMenu(TrayMenu, 0,  Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING | (keepDisplayOn ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED) | (mode == AwakeMode.PASSIVE ? Native.Constants.MF_DISABLED : Native.Constants.MF_ENABLED), (uint)TrayCommands.TC_DISPLAY_SETTING, Resources.AWAKE_KEEP_SCREEN_ON);
            }

            // In case there are no tray shortcuts defined for the application default to a
            // reasonable initial set.
            if (trayTimeShortcuts.Count == 0)
            {
                trayTimeShortcuts.AddRange(Manager.GetDefaultTrayOptions());
            }

            var awakeTimeMenu = Bridge.CreatePopupMenu();
            for (int i = 0; i < trayTimeShortcuts.Count; i++)
            {
                Bridge.InsertMenu(awakeTimeMenu, (uint)i, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING, (uint)TrayCommands.TC_TIME + (uint)i, trayTimeShortcuts.ElementAt(i).Key);
            }

            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_SEPARATOR, 0, string.Empty);

            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING | (mode == AwakeMode.PASSIVE ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_PASSIVE, Resources.AWAKE_OFF);
            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING | (mode == AwakeMode.INDEFINITE ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_INDEFINITE, Resources.AWAKE_KEEP_INDEFINITELY);
            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_POPUP | (mode == AwakeMode.TIMED ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)awakeTimeMenu, Resources.AWAKE_KEEP_ON_INTERVAL);
            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING | Native.Constants.MF_DISABLED | (mode == AwakeMode.EXPIRABLE ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_EXPIRABLE, Resources.AWAKE_KEEP_UNTIL_EXPIRATION);

            TrayIcon.Text = text;
        }

        private sealed class CheckButtonToolStripMenuItemAccessibleObject : ToolStripItem.ToolStripItemAccessibleObject
        {
            private readonly CheckButtonToolStripMenuItem _menuItem;

            public CheckButtonToolStripMenuItemAccessibleObject(CheckButtonToolStripMenuItem menuItem)
                : base(menuItem)
            {
                _menuItem = menuItem;
            }

            public override AccessibleRole Role => AccessibleRole.CheckButton;

            public override string Name => _menuItem.Text + ", " + Role + ", " + (_menuItem.Checked ? Resources.AWAKE_CHECKED : Resources.AWAKE_UNCHECKED);
        }

        private sealed class CheckButtonToolStripMenuItem : ToolStripMenuItem
        {
            protected override AccessibleObject CreateAccessibilityInstance()
            {
                return new CheckButtonToolStripMenuItemAccessibleObject(this);
            }
        }
    }
}
