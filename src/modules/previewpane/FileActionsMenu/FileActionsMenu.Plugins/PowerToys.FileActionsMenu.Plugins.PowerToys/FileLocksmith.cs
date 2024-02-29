// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PowerToys.FileActionsMenu.Plugins.PowerToys
{
    internal sealed class FileLocksmith : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => "What's locking this file?";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 3;

        public IconElement? Icon => IconHelper.GetIconFromModuleName("FileLocksmith");

        public bool IsVisible => true;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            SettingsUtils fileLocksmithSettings = new();

            string paths = string.Join("\n", SelectedItems);
            paths += "\n";

            File.WriteAllText(fileLocksmithSettings.GetSettingsFilePath("File Locksmith", "last-run.log"), string.Empty);
            using (BinaryWriter streamWriter = new(File.Open(fileLocksmithSettings.GetSettingsFilePath("File Locksmith", "last-run.log"), FileMode.OpenOrCreate), Encoding.ASCII))
            {
                foreach (char c in paths)
                {
                    streamWriter.Write((ushort)c);
                }
            }

            Process.Start("PowerToys.FileLocksmithUI.exe");
            return Task.CompletedTask;
        }
    }
}
