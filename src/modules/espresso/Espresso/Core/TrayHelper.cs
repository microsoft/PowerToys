// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
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
                settings.Properties.KeepDisplayOn,
                settings.Properties.Mode,
                PassiveKeepAwakeCallback(text),
                IndefiniteKeepAwakeCallback(text),
                TimedKeepAwakeCallback(text),
                KeepDisplayOnCallback(text),
                ExitCallback());
        }

        private static Action ExitCallback()
        {
            return () =>
            {
                Environment.Exit(0);
            };
        }

        private static Action KeepDisplayOnCallback(string moduleName)
        {
            return () =>
            {
                EspressoSettings currentSettings;

                try
                {
                    currentSettings = ModuleSettings.GetSettings<EspressoSettings>(moduleName);
                }
                catch (FileNotFoundException)
                {
                    currentSettings = new EspressoSettings();
                }

                currentSettings.Properties.KeepDisplayOn = !currentSettings.Properties.KeepDisplayOn;

                ModuleSettings.SaveSettings(JsonSerializer.Serialize(currentSettings), moduleName);
            };
        }

        private static Action<uint, uint> TimedKeepAwakeCallback(string moduleName)
        {
            return (hours, minutes) =>
            {
                EspressoSettings currentSettings;

                try
                {
                    currentSettings = ModuleSettings.GetSettings<EspressoSettings>(moduleName);
                }
                catch (FileNotFoundException)
                {
                    currentSettings = new EspressoSettings();
                }

                currentSettings.Properties.Mode = EspressoMode.TIMED;
                currentSettings.Properties.Hours = hours;
                currentSettings.Properties.Minutes = minutes;

                ModuleSettings.SaveSettings(JsonSerializer.Serialize(currentSettings), moduleName);
            };
        }

        private static Action PassiveKeepAwakeCallback(string moduleName)
        {
            return () =>
            {
                EspressoSettings currentSettings;

                try
                {
                    currentSettings = ModuleSettings.GetSettings<EspressoSettings>(moduleName);
                }
                catch (FileNotFoundException)
                {
                    currentSettings = new EspressoSettings();
                }

                currentSettings.Properties.Mode = EspressoMode.PASSIVE;

                ModuleSettings.SaveSettings(JsonSerializer.Serialize(currentSettings), moduleName);
            };
        }

        private static Action IndefiniteKeepAwakeCallback(string moduleName)
        {
            return () =>
            {
                EspressoSettings currentSettings;

                try
                {
                    currentSettings = ModuleSettings.GetSettings<EspressoSettings>(moduleName);
                }
                catch (FileNotFoundException)
                {
                    currentSettings = new EspressoSettings();
                }

                currentSettings.Properties.Mode = EspressoMode.INDEFINITE;

                ModuleSettings.SaveSettings(JsonSerializer.Serialize(currentSettings), moduleName);
            };
        }

        public static void SetTray(string text, bool keepDisplayOn, EspressoMode mode, Action passiveKeepAwakeCallback, Action indefiniteKeepAwakeCallback, Action<uint, uint> timedKeepAwakeCallback, Action keepDisplayOnCallback, Action exitCallback)
        {
            var contextMenuStrip = new ContextMenuStrip();

            // Main toolstrip.
            var operationContextMenu = new ToolStripMenuItem
            {
                Text = "Mode",
            };

            // No keep-awake menu item.
            var passiveMenuItem = new ToolStripMenuItem
            {
                Text = "Off (Passive)",
            };

            if (mode == EspressoMode.PASSIVE)
            {
                passiveMenuItem.Checked = true;
            }
            else
            {
                passiveMenuItem.Checked = false;
            }

            passiveMenuItem.Click += (e, s) =>
            {
                // User opted to set the mode to indefinite, so we need to write new settings.
                passiveKeepAwakeCallback();
            };

            // Indefinite keep-awake menu item.
            var indefiniteMenuItem = new ToolStripMenuItem
            {
                Text = "Keep awake indefinitely",
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
                Text = "Keep screen on",
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
                Text = "Keep awake temporarily",
            };
            if (mode == EspressoMode.TIMED)
            {
                timedMenuItem.Checked = true;
            }
            else
            {
                timedMenuItem.Checked = false;
            }

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

            // Exit menu item.
            var exitContextMenu = new ToolStripMenuItem
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
