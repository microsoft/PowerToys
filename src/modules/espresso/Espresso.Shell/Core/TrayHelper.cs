using Espresso.Shell.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
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
        }

        internal static void InitializeEspressoTray(EspressoMode mode, bool keepDisplayOn, Action indefiniteSelectionCallback, Action timedSelectionCallback)
        {
            var contextMenuStrip = new ContextMenuStrip();

            // Main toolstrip.
            var operationContextMenu = new ToolStrip();
            operationContextMenu.Text = "Mode";

            // Indefinite keep-awake menu item.
            var indefiniteMenuItem = new ToolStripMenuItem();
            indefiniteMenuItem.Text = "Indefinite";
            if (mode == EspressoMode.INDEFINITE)
            {
                indefiniteMenuItem.Checked = true;
            }
            operationContextMenu.Items.Add(indefiniteMenuItem);
        }
    }
}
