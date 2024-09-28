// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;

namespace PowerToys.FileActionsMenu.Plugins.ExecutableActions
{
    internal sealed class Uninstall : IAction
    {
        private string[]? _selectedItems;
        private string? _uninstallerPath;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("Executable_Actions.Uninstall.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 2;

        public IconElement? Icon => new FontIcon { Glyph = "\ue74d" };

        public bool IsVisible => SelectedItems.Length == 1
            && (SelectedItems[0].EndsWith(".exe", StringComparison.InvariantCulture) || ShortcutHelper.GetFullPathFromShortcut(SelectedItems[0])
            .EndsWith(".exe", StringComparison.InvariantCulture) || SelectedItems[0].EndsWith(".dll", StringComparison.InvariantCulture) || ShortcutHelper.GetFullPathFromShortcut(SelectedItems[0]).EndsWith(".dll", StringComparison.InvariantCulture))
            && ((_uninstallerPath = GetUninstallerPath(ShortcutHelper.GetFullPathFromShortcut(SelectedItems[0]))) is not null);

        public Task Execute(object sender, RoutedEventArgs e)
        {
            if (_uninstallerPath is null)
            {
                return Task.CompletedTask;
            }

            FileActionsMenuUninstallActionInvokedEvent telemetryEvent = new()
            {
                IsCalledFromDesktop = SelectedItems[0].Contains(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)) || SelectedItems[0].Contains(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)) || SelectedItems[0].Contains(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
                IsCalledOnShortcut = SelectedItems[0].EndsWith(".lnk", StringComparison.InvariantCulture),
            };
            TelemetryHelper.LogEvent(telemetryEvent, SelectedItems);

            // Thank you Microsoft Copilot!
            static string[] SplitCommandLine(string commandLine)
            {
                return commandLine.Split('"')
                    .Select((element, index) => index % 2 == 0
                        ? element.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        : [element])
                    .SelectMany(element => element)
                    .ToArray();
            }

            ProcessStartInfo processStartInfo = new()
            {
                FileName = SplitCommandLine(_uninstallerPath)[0],
                Arguments = string.Join(" ", SplitCommandLine(_uninstallerPath).Skip(1)),
                UseShellExecute = true,
            };
            Process.Start(processStartInfo);
            return Task.CompletedTask;
        }

        public string? GetUninstallerPath(string exePath)
        {
            string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using RegistryKey? rk = Registry.LocalMachine.OpenSubKey(uninstallKey);
            if (rk == null)
            {
                return null;
            }

            foreach (string skName in rk.GetSubKeyNames())
            {
                using RegistryKey? sk = rk.OpenSubKey(skName);
                if (sk == null)
                {
                    return null;
                }

                try
                {
                    string? value = sk.GetValue("InstallLocation")?.ToString();
                    if (value is null || value == string.Empty)
                    {
                        continue;
                    }

                    if ((Path.GetDirectoryName(exePath) ?? string.Empty).Contains(value.TrimEnd('\\')))
                    {
                        return sk.GetValue("UninstallString")?.ToString();
                    }
                }
                catch (Exception)
                {
                    // Ignore exceptions
                }
            }

            return null;
        }
    }
}
