// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Awake.Core.Models;
using Microsoft.PowerToys.Settings.UI.Library;
using NLog;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace Awake.Core
{
    internal static class TrayHelper
    {
        private static readonly Logger _log;

        private static DestroyMenuSafeHandle TrayMenu { get; set; }

        private static NotifyIcon TrayIcon { get; set; }

        static TrayHelper()
        {
            _log = LogManager.GetCurrentClassLogger();
            TrayMenu = new DestroyMenuSafeHandle();
            TrayIcon = new NotifyIcon();
        }

        public static void InitializeTray(string text, Icon icon, ManualResetEvent? exitSignal, ContextMenuStrip? contextMenu = null)
        {
            Task.Factory.StartNew(
                (tray) =>
                {
                    try
                    {
                        _log.Info("Setting up the tray.");
                        ((NotifyIcon?)tray).Text = text;
                        ((NotifyIcon?)tray).Icon = icon;
                        ((NotifyIcon?)tray).ContextMenuStrip = contextMenu;
                        ((NotifyIcon?)tray).Visible = true;
                        ((NotifyIcon?)tray).MouseClick += TrayClickHandler;
                        Application.AddMessageFilter(new TrayMessageFilter(exitSignal));
                        Application.Run();
                        _log.Info("Tray setup complete.");
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"An error occurred initializing the tray. {ex.Message}");
                        _log.Error($"{ex.StackTrace}");
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
            HWND windowHandle = APIHelper.GetHiddenWindow();

            if (windowHandle != HWND.Null)
            {
                PInvoke.SetForegroundWindow(windowHandle);
                PInvoke.TrackPopupMenuEx(TrayMenu, 0, Cursor.Position.X, Cursor.Position.Y, windowHandle, null);
            }
        }

        internal static void SetTray(string text, AwakeSettings settings, bool startedFromPowerToys)
        {
            SetTray(
                text,
                settings.Properties.KeepDisplayOn,
                settings.Properties.Mode,
                settings.Properties.TrayTimeShortcuts,
                startedFromPowerToys);
        }

        public static void SetTray(string text, bool keepDisplayOn, AwakeMode mode, Dictionary<string, int> trayTimeShortcuts, bool startedFromPowerToys)
        {
            TrayMenu = new DestroyMenuSafeHandle(PInvoke.CreatePopupMenu());

            if (!TrayMenu.IsInvalid)
            {
                if (!startedFromPowerToys)
                {
                    // If Awake is started from PowerToys, the correct way to exit it is disabling it from Settings.
                    PInvoke.InsertMenu(TrayMenu, 0, MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING, (uint)TrayCommands.TC_EXIT, "Exit");
                    PInvoke.InsertMenu(TrayMenu, 0, MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_SEPARATOR, 0, string.Empty);
                }

                PInvoke.InsertMenu(TrayMenu, 0, MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING | (keepDisplayOn ? MENU_ITEM_FLAGS.MF_CHECKED : MENU_ITEM_FLAGS.MF_UNCHECKED) | (mode == AwakeMode.PASSIVE ? MENU_ITEM_FLAGS.MF_DISABLED : MENU_ITEM_FLAGS.MF_ENABLED), (uint)TrayCommands.TC_DISPLAY_SETTING, "Keep screen on");
            }

            // In case there are no tray shortcuts defined for the application default to a
            // reasonable initial set.
            if (trayTimeShortcuts.Count == 0)
            {
                trayTimeShortcuts.AddRange(APIHelper.GetDefaultTrayOptions());
            }

            // TODO: Make sure that this loads from JSON instead of being hard-coded.
            var awakeTimeMenu = new DestroyMenuSafeHandle(PInvoke.CreatePopupMenu(), false);
            for (int i = 0; i < trayTimeShortcuts.Count; i++)
            {
                PInvoke.InsertMenu(awakeTimeMenu, (uint)i, MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING, (uint)TrayCommands.TC_TIME + (uint)i, trayTimeShortcuts.ElementAt(i).Key);
            }

            var modeMenu = new DestroyMenuSafeHandle(PInvoke.CreatePopupMenu(), false);
            PInvoke.InsertMenu(modeMenu, 0, MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING | (mode == AwakeMode.PASSIVE ? MENU_ITEM_FLAGS.MF_CHECKED : MENU_ITEM_FLAGS.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_PASSIVE, "Off (keep using the selected power plan)");
            PInvoke.InsertMenu(modeMenu, 1, MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING | (mode == AwakeMode.INDEFINITE ? MENU_ITEM_FLAGS.MF_CHECKED : MENU_ITEM_FLAGS.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_INDEFINITE, "Keep awake indefinitely");

            PInvoke.InsertMenu(modeMenu, 2, MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_POPUP | (mode == AwakeMode.TIMED ? MENU_ITEM_FLAGS.MF_CHECKED : MENU_ITEM_FLAGS.MF_UNCHECKED), (uint)awakeTimeMenu.DangerousGetHandle(), "Keep awake temporarily");
            PInvoke.InsertMenu(TrayMenu, 0, MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_POPUP, (uint)modeMenu.DangerousGetHandle(), "Mode");

            TrayIcon.Text = text;
        }

        private sealed class CheckButtonToolStripMenuItemAccessibleObject : ToolStripItem.ToolStripItemAccessibleObject
        {
            private CheckButtonToolStripMenuItem _menuItem;

            public CheckButtonToolStripMenuItemAccessibleObject(CheckButtonToolStripMenuItem menuItem)
                : base(menuItem)
            {
                _menuItem = menuItem;
            }

            public override AccessibleRole Role
            {
                get
                {
                    return AccessibleRole.CheckButton;
                }
            }

            public override string Name => _menuItem.Text + ", " + Role + ", " + (_menuItem.Checked ? "Checked" : "Unchecked");
        }

        private sealed class CheckButtonToolStripMenuItem : ToolStripMenuItem
        {
            public CheckButtonToolStripMenuItem()
            {
            }

            protected override AccessibleObject CreateAccessibilityInstance()
            {
                return new CheckButtonToolStripMenuItemAccessibleObject(this);
            }
        }
    }
}
