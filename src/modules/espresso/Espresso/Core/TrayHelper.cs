// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.PowerToys.Settings.UI.Library;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8603 // Possible null reference return.

namespace Espresso.Shell.Core
{
    internal static class TrayHelper
    {
        private static NotifyIcon? trayIcon;

        private static NotifyIcon TrayIcon { get => trayIcon; set => trayIcon = value; }

        private static SettingsUtils? moduleSettings;

        private static SettingsUtils ModuleSettings { get => moduleSettings; set => moduleSettings = value; }

        static TrayHelper()
        {
            TrayIcon = new NotifyIcon();
            ModuleSettings = new SettingsUtils();
        }

        public static void InitializeTray(string text, Icon icon, ContextMenuStrip? contextMenu = null)
        {
            System.Threading.Tasks.Task.Factory.StartNew(
                (tray) =>
            {
                ((NotifyIcon?)tray).Text = text;
                ((NotifyIcon?)tray).Icon = icon;
                ((NotifyIcon?)tray).ContextMenuStrip = contextMenu;
                ((NotifyIcon?)tray).Visible = true;

                Application.Run();
            }, TrayIcon);
        }

        internal static void SetTray(string text, EspressoSettings settings)
        {
            SetTray(
                text,
                settings.Properties.KeepDisplayOn.Value,
                settings.Properties.Mode,
                IndefiniteKeepAwakeCallback(text),
                TimedKeepAwakeCallback(text),
                KeepDisplayOnCallback(text));
        }

        private static Action KeepDisplayOnCallback(string text)
        {
            return () =>
            {
                // Just changing the display mode.
                var currentSettings = ModuleSettings.GetSettings<EspressoSettings>(text);
                currentSettings.Properties.KeepDisplayOn.Value = !currentSettings.Properties.KeepDisplayOn.Value;

                ModuleSettings.SaveSettings(JsonSerializer.Serialize(currentSettings), text);
            };
        }

        private static Action<int, int> TimedKeepAwakeCallback(string text)
        {
            return (hours, minutes) =>
            {
                // Set timed keep awake.
                var currentSettings = ModuleSettings.GetSettings<EspressoSettings>(text);
                currentSettings.Properties.Mode = EspressoMode.TIMED;
                currentSettings.Properties.Hours.Value = hours;
                currentSettings.Properties.Minutes.Value = minutes;

                ModuleSettings.SaveSettings(JsonSerializer.Serialize(currentSettings), text);
            };
        }

        private static Action IndefiniteKeepAwakeCallback(string text)
        {
            return () =>
            {
                // Set indefinite keep awake.
                var currentSettings = ModuleSettings.GetSettings<EspressoSettings>(text);
                currentSettings.Properties.Mode = EspressoMode.INDEFINITE;

                ModuleSettings.SaveSettings(JsonSerializer.Serialize(currentSettings), text);
            };
        }

        internal static void SetTray(string text, bool keepDisplayOn, EspressoMode mode, Action indefiniteKeepAwakeCallback, Action<int, int> timedKeepAwakeCallback, Action keepDisplayOnCallback)
        {
            var contextMenuStrip = new ContextMenuStrip();

            // Main toolstrip.
            var operationContextMenu = new ToolStripMenuItem
            {
                Text = "Mode",
            };

            // Indefinite keep-awake menu item.
            var indefiniteMenuItem = new ToolStripMenuItem
            {
                Text = "Indefinite",
            };

            if (mode == EspressoMode.INDEFINITE)
            {
                indefiniteMenuItem.Checked = true;
            }
            else
            {
                indefiniteMenuItem.Checked = false;
            }

            indefiniteMenuItem.Click += (e, s) =>
            {
                // User opted to set the mode to indefinite, so we need to write new settings.
                indefiniteKeepAwakeCallback();
            };

            var displayOnMenuItem = new ToolStripMenuItem
            {
                Text = "Keep display on",
            };
            if (keepDisplayOn)
            {
                displayOnMenuItem.Checked = true;
            }
            else
            {
                displayOnMenuItem.Checked = false;
            }

            displayOnMenuItem.Click += (e, s) =>
            {
                // User opted to set the display mode directly.
                keepDisplayOnCallback();
            };

            // Timed keep-awake menu item
            var timedMenuItem = new ToolStripMenuItem
            {
                Text = "Timed",
            };

            var halfHourMenuItem = new ToolStripMenuItem
            {
                Text = "30 minutes",
            };
            halfHourMenuItem.Click += (e, s) =>
            {
                // User is setting the keep-awake to 30 minutes.
                timedKeepAwakeCallback(0, 30);
            };

            var oneHourMenuItem = new ToolStripMenuItem
            {
                Text = "1 hour",
            };
            oneHourMenuItem.Click += (e, s) =>
            {
                // User is setting the keep-awake to 1 hour.
                timedKeepAwakeCallback(1, 0);
            };

            var twoHoursMenuItem = new ToolStripMenuItem
            {
                Text = "2 hours",
            };
            twoHoursMenuItem.Click += (e, s) =>
            {
                // User is setting the keep-awake to 2 hours.
                timedKeepAwakeCallback(2, 0);
            };

            timedMenuItem.DropDownItems.Add(halfHourMenuItem);
            timedMenuItem.DropDownItems.Add(oneHourMenuItem);
            timedMenuItem.DropDownItems.Add(twoHoursMenuItem);

            operationContextMenu.DropDownItems.Add(indefiniteMenuItem);
            operationContextMenu.DropDownItems.Add(timedMenuItem);
            operationContextMenu.DropDownItems.Add(new ToolStripSeparator());
            operationContextMenu.DropDownItems.Add(displayOnMenuItem);

            contextMenuStrip.Items.Add(operationContextMenu);

            TrayIcon.Text = text;
            TrayIcon.ContextMenuStrip = contextMenuStrip;
        }
    }
}
