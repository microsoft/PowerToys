// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.PowerToys.Settings.UI.Library;
using NLog;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8603 // Possible null reference return.

namespace Awake.Core
{
    internal static class TrayHelper
    {
        private static readonly Logger _log;

        private static NotifyIcon? _trayIcon;

        private static NotifyIcon TrayIcon { get => _trayIcon; set => _trayIcon = value; }

        private static SettingsUtils? _moduleSettings;

        private static SettingsUtils ModuleSettings { get => _moduleSettings; set => _moduleSettings = value; }

        static TrayHelper()
        {
            _log = LogManager.GetCurrentClassLogger();
            TrayIcon = new NotifyIcon();
            ModuleSettings = new SettingsUtils();
        }

        public static void InitializeTray(string text, Icon icon, ContextMenuStrip? contextMenu = null)
        {
            Task.Factory.StartNew(
                (tray) =>
                {
                    ((NotifyIcon?)tray).Text = text;
                    ((NotifyIcon?)tray).Icon = icon;
                    ((NotifyIcon?)tray).ContextMenuStrip = contextMenu;
                    ((NotifyIcon?)tray).Visible = true;

                    _log.Info("Setting up the tray.");
                    Application.Run();
                    _log.Info("Tray setup complete.");
                }, TrayIcon);
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
                PassiveKeepAwakeCallback(InternalConstants.AppName),
                IndefiniteKeepAwakeCallback(InternalConstants.AppName),
                TimedKeepAwakeCallback(InternalConstants.AppName),
                KeepDisplayOnCallback(InternalConstants.AppName),
                ExitCallback());
        }

        private static Action ExitCallback()
        {
            return () =>
            {
                Environment.Exit(Environment.ExitCode);
            };
        }

        private static Action KeepDisplayOnCallback(string moduleName)
        {
            return () =>
            {
                AwakeSettings currentSettings;

                try
                {
                    currentSettings = ModuleSettings.GetSettings<AwakeSettings>(moduleName);
                }
                catch (FileNotFoundException)
                {
                    currentSettings = new AwakeSettings();
                }

                currentSettings.Properties.KeepDisplayOn = !currentSettings.Properties.KeepDisplayOn;

                ModuleSettings.SaveSettings(JsonSerializer.Serialize(currentSettings), moduleName);
            };
        }

        private static Action<uint, uint> TimedKeepAwakeCallback(string moduleName)
        {
            return (hours, minutes) =>
            {
                AwakeSettings currentSettings;

                try
                {
                    currentSettings = ModuleSettings.GetSettings<AwakeSettings>(moduleName);
                }
                catch (FileNotFoundException)
                {
                    currentSettings = new AwakeSettings();
                }

                currentSettings.Properties.Mode = AwakeMode.TIMED;
                currentSettings.Properties.Hours = hours;
                currentSettings.Properties.Minutes = minutes;

                ModuleSettings.SaveSettings(JsonSerializer.Serialize(currentSettings), moduleName);
            };
        }

        private static Action PassiveKeepAwakeCallback(string moduleName)
        {
            return () =>
            {
                AwakeSettings currentSettings;

                try
                {
                    currentSettings = ModuleSettings.GetSettings<AwakeSettings>(moduleName);
                }
                catch (FileNotFoundException)
                {
                    currentSettings = new AwakeSettings();
                }

                currentSettings.Properties.Mode = AwakeMode.PASSIVE;

                ModuleSettings.SaveSettings(JsonSerializer.Serialize(currentSettings), moduleName);
            };
        }

        private static Action IndefiniteKeepAwakeCallback(string moduleName)
        {
            return () =>
            {
                AwakeSettings currentSettings;

                try
                {
                    currentSettings = ModuleSettings.GetSettings<AwakeSettings>(moduleName);
                }
                catch (FileNotFoundException)
                {
                    currentSettings = new AwakeSettings();
                }

                currentSettings.Properties.Mode = AwakeMode.INDEFINITE;

                ModuleSettings.SaveSettings(JsonSerializer.Serialize(currentSettings), moduleName);
            };
        }

        public static void SetTray(string text, bool keepDisplayOn, AwakeMode mode, Action passiveKeepAwakeCallback, Action indefiniteKeepAwakeCallback, Action<uint, uint> timedKeepAwakeCallback, Action keepDisplayOnCallback, Action exitCallback)
        {
            ContextMenuStrip? contextMenuStrip = new ContextMenuStrip();

            // Main toolstrip.
            ToolStripMenuItem? operationContextMenu = new ToolStripMenuItem
            {
                Text = "Mode",
            };

            // No keep-awake menu item.
            ToolStripMenuItem? passiveMenuItem = new ToolStripMenuItem
            {
                Text = "Off (Passive)",
            };

            passiveMenuItem.Checked = mode == AwakeMode.PASSIVE;

            passiveMenuItem.Click += (e, s) =>
            {
                // User opted to set the mode to indefinite, so we need to write new settings.
                passiveKeepAwakeCallback();
            };

            // Indefinite keep-awake menu item.
            ToolStripMenuItem? indefiniteMenuItem = new ToolStripMenuItem
            {
                Text = "Keep awake indefinitely",
            };

            indefiniteMenuItem.Checked = mode == AwakeMode.INDEFINITE;

            indefiniteMenuItem.Click += (e, s) =>
            {
                // User opted to set the mode to indefinite, so we need to write new settings.
                indefiniteKeepAwakeCallback();
            };

            ToolStripMenuItem? displayOnMenuItem = new ToolStripMenuItem
            {
                Text = "Keep screen on",
            };

            displayOnMenuItem.Checked = keepDisplayOn;

            displayOnMenuItem.Click += (e, s) =>
            {
                // User opted to set the display mode directly.
                keepDisplayOnCallback();
            };

            // Timed keep-awake menu item
            ToolStripMenuItem? timedMenuItem = new ToolStripMenuItem
            {
                Text = "Keep awake temporarily",
            };

            timedMenuItem.Checked = mode == AwakeMode.TIMED;

            ToolStripMenuItem? halfHourMenuItem = new ToolStripMenuItem
            {
                Text = "30 minutes",
            };

            halfHourMenuItem.Click += (e, s) =>
            {
                // User is setting the keep-awake to 30 minutes.
                timedKeepAwakeCallback(0, 30);
            };

            ToolStripMenuItem? oneHourMenuItem = new ToolStripMenuItem
            {
                Text = "1 hour",
            };

            oneHourMenuItem.Click += (e, s) =>
            {
                // User is setting the keep-awake to 1 hour.
                timedKeepAwakeCallback(1, 0);
            };

            ToolStripMenuItem? twoHoursMenuItem = new ToolStripMenuItem
            {
                Text = "2 hours",
            };

            twoHoursMenuItem.Click += (e, s) =>
            {
                // User is setting the keep-awake to 2 hours.
                timedKeepAwakeCallback(2, 0);
            };

            // Exit menu item.
            ToolStripMenuItem? exitContextMenu = new ToolStripMenuItem
            {
                Text = "Exit",
            };

            exitContextMenu.Click += (e, s) =>
            {
                // User is setting the keep-awake to 2 hours.
                exitCallback();
            };

            timedMenuItem.DropDownItems.Add(halfHourMenuItem);
            timedMenuItem.DropDownItems.Add(oneHourMenuItem);
            timedMenuItem.DropDownItems.Add(twoHoursMenuItem);

            operationContextMenu.DropDownItems.Add(passiveMenuItem);
            operationContextMenu.DropDownItems.Add(indefiniteMenuItem);
            operationContextMenu.DropDownItems.Add(timedMenuItem);

            contextMenuStrip.Items.Add(operationContextMenu);
            contextMenuStrip.Items.Add(displayOnMenuItem);
            contextMenuStrip.Items.Add(new ToolStripSeparator());
            contextMenuStrip.Items.Add(exitContextMenu);

            TrayIcon.Text = text;
            TrayIcon.ContextMenuStrip = contextMenuStrip;
        }
    }
}
