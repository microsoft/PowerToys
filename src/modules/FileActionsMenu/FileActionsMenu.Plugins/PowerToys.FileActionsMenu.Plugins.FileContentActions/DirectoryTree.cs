// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.FileContentActions
{
    internal sealed class DirectoryTree : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("File_Content_Actions.CopyDirectoryTree.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 2;

        public IconElement? Icon => new FontIcon() { Glyph = "\ue8b7" };

        public bool IsVisible => SelectedItems.Length == 1 && Directory.Exists(SelectedItems[0]);

        public Task Execute(object sender, RoutedEventArgs e)
        {
            TelemetryHelper.LogEvent(new FileActionsMenuCopyFolderTreeActionInvokedEvent() { IsDriveRoot = SelectedItems[0].EndsWith(":/", StringComparison.InvariantCulture) }, SelectedItems);

            Process process = new();

            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/c tree {SelectedItems[0]} /f";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;

            process.Start();

            string output = process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            System.Windows.Clipboard.SetText(output);

            return Task.CompletedTask;
        }
    }
}
