// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Awake.Core.Models;
using Microsoft.PowerToys.Settings.UI.Library;
using NLog;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8603 // Possible null reference return.

namespace Awake.Core
{
    internal static class TrayHelper
    {
        private static readonly Logger _log;

        private static IntPtr _trayMenu;

        private static IntPtr TrayMenu { get => _trayMenu; set => _trayMenu = value; }

        private static NotifyIcon? _trayIcon;

        private static NotifyIcon TrayIcon { get => _trayIcon; set => _trayIcon = value; }

        static TrayHelper()
        {
            _log = LogManager.GetCurrentClassLogger();
            TrayIcon = new NotifyIcon();
        }

        public static void InitializeTray(string text, Icon icon, ContextMenuStrip? contextMenu = null)
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
                        Application.AddMessageFilter(new TrayMessageFilter());
                        Application.Run();
                        _log.Info("Tray setup complete.");
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"An error occurred initializing the tray. {ex.Message}");
                        _log.Error($"{ex.StackTrace}");
                    }
                }, TrayIcon);
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
            IntPtr windowHandle = APIHelper.GetHiddenWindow();

            if (windowHandle != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(windowHandle);
                NativeMethods.TrackPopupMenuEx(TrayMenu, 0, Cursor.Position.X, Cursor.Position.Y, windowHandle, IntPtr.Zero);
            }
        }

        public static void ClearTray()
        {
            TrayIcon.Icon = null;
            TrayIcon.Dispose();
        }

        internal static void SetTray(string text, AwakeSettings settings)
        {
            SetTray(
                text,
                settings.Properties.KeepDisplayOn,
                settings.Properties.Mode,
                settings.Properties.TrayTimeShortcuts);
        }

        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1005:Single line comments should begin with single space", Justification = "For debugging purposes - will remove later.")]
        public static void SetTray(string text, bool keepDisplayOn, AwakeMode mode, Dictionary<string, int> trayTimeShortcuts)
        {
            if (TrayMenu != IntPtr.Zero)
            {
                var destructionStatus = NativeMethods.DestroyMenu(TrayMenu);
                if (destructionStatus != true)
                {
                    _log.Error("Failed to destroy tray menu and free up memory.");
                }
            }

            TrayMenu = NativeMethods.CreatePopupMenu();
            NativeMethods.InsertMenu(TrayMenu, 0, NativeConstants.MF_BYPOSITION | NativeConstants.MF_STRING, (uint)TrayCommands.TC_EXIT, "Exit");
            NativeMethods.InsertMenu(TrayMenu, 0, NativeConstants.MF_BYPOSITION | NativeConstants.MF_SEPARATOR, 0, string.Empty);
            NativeMethods.InsertMenu(TrayMenu, 0, NativeConstants.MF_BYPOSITION | NativeConstants.MF_STRING | (keepDisplayOn ? NativeConstants.MF_CHECKED : NativeConstants.MF_UNCHECKED), (uint)TrayCommands.TC_DISPLAY_SETTING, "Keep screen on");

            // In case there are no tray shortcuts defined for the application default to a
            // reasonable initial set.
            if (trayTimeShortcuts.Count == 0)
            {
                trayTimeShortcuts.AddRange(APIHelper.GetDefaultTrayOptions());
            }

            // TODO: Make sure that this loads from JSON instead of being hard-coded.
            var awakeTimeMenu = NativeMethods.CreatePopupMenu();
            for (int i = 0; i < trayTimeShortcuts.Count; i++)
            {
                NativeMethods.InsertMenu(awakeTimeMenu, (uint)i, NativeConstants.MF_BYPOSITION | NativeConstants.MF_STRING, (uint)TrayCommands.TC_TIME + (uint)i, trayTimeShortcuts.ElementAt(i).Key);
            }

            var modeMenu = NativeMethods.CreatePopupMenu();
            NativeMethods.InsertMenu(modeMenu, 0, NativeConstants.MF_BYPOSITION | NativeConstants.MF_STRING | (mode == AwakeMode.PASSIVE ? NativeConstants.MF_CHECKED : NativeConstants.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_PASSIVE, "Off (keep using the selected power plan)");
            NativeMethods.InsertMenu(modeMenu, 1, NativeConstants.MF_BYPOSITION | NativeConstants.MF_STRING | (mode == AwakeMode.INDEFINITE ? NativeConstants.MF_CHECKED : NativeConstants.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_INDEFINITE, "Keep awake indefinitely");

            NativeMethods.InsertMenu(modeMenu, 2, NativeConstants.MF_BYPOSITION | NativeConstants.MF_POPUP | (mode == AwakeMode.TIMED ? NativeConstants.MF_CHECKED : NativeConstants.MF_UNCHECKED), (uint)awakeTimeMenu, "Keep awake temporarily");
            NativeMethods.InsertMenu(TrayMenu, 0, NativeConstants.MF_BYPOSITION | NativeConstants.MF_POPUP, (uint)modeMenu, "Mode");

            TrayIcon.Text = text;
        }

        private class CheckButtonToolStripMenuItemAccessibleObject : ToolStripItem.ToolStripItemAccessibleObject
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

        private class CheckButtonToolStripMenuItem : ToolStripMenuItem
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
