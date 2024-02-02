// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.PowerToys.Settings.UI.Library;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    internal sealed class FileLocksmith : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Header => "What's locking this file?";

        public bool HasSubMenu => false;

        public IAction[]? SubMenuItems => null;

        public int Category => 4;

        public IconElement? Icon => null;

        public bool IsVisible => true;

        public void Execute(object sender, RoutedEventArgs e)
        {
            SettingsUtils fileLocksmithSettings = new SettingsUtils();

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

            Process.Start("WinUI3Apps\\PowerToys.FileLocksmithUI.exe");
        }
    }
}
