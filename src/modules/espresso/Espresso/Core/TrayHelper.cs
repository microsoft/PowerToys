// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Espresso.Shell.Models;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Espresso.Shell.Core
{
    internal static class TrayHelper
    {
        static NotifyIcon trayIcon;
        static TrayHelper()
        {
            trayIcon = new NotifyIcon();
        }

        private static void InitializeTrayIcon(string text, Icon icon, ContextMenuStrip contextMenu)
        {
            trayIcon.Text = text;
            trayIcon.Icon = icon;
            trayIcon.ContextMenuStrip = contextMenu;
            trayIcon.Visible = true;

            Application.Run();
        }

        internal static void InitializeEspressoTray(string text, EspressoMode mode, bool keepDisplayOn, Action indefiniteSelectionCallback, Action timedSelectionCallback)
        {
            var contextMenuStrip = new ContextMenuStrip();
          
            // Main toolstrip.
            var operationContextMenu = new ToolStripMenuItem();
            operationContextMenu.Text = "Mode";

            // Indefinite keep-awake menu item.
            var indefiniteMenuItem = new ToolStripMenuItem();
            indefiniteMenuItem.Text = "Indefinite";
            if (mode == EspressoMode.INDEFINITE)
            {
                indefiniteMenuItem.Checked = true;
            }

            // Timed keep-awake menu item
            var timedMenuItem = new ToolStripMenuItem();
            timedMenuItem.Text = "Timed";

            var halfHourMenuItem = new ToolStripMenuItem();
            halfHourMenuItem.Text = "30 minutes";

            var oneHourMenuItem = new ToolStripMenuItem();
            oneHourMenuItem.Text = "1 hour";

            var twoHoursMenuItem = new ToolStripMenuItem();
            twoHoursMenuItem.Text = "2 hours";

            timedMenuItem.DropDownItems.Add(halfHourMenuItem);
            timedMenuItem.DropDownItems.Add(oneHourMenuItem);
            timedMenuItem.DropDownItems.Add(twoHoursMenuItem);

            operationContextMenu.DropDownItems.Add(indefiniteMenuItem);
            operationContextMenu.DropDownItems.Add(timedMenuItem);

            contextMenuStrip.Items.Add(operationContextMenu);

#pragma warning disable CS8604 // Possible null reference argument.
            Task.Factory.StartNew(() => InitializeTrayIcon(text, APIHelper.Extract("shell32.dll", 42, true), contextMenuStrip));
#pragma warning restore CS8604 // Possible null reference argument.
        }
    }
}
