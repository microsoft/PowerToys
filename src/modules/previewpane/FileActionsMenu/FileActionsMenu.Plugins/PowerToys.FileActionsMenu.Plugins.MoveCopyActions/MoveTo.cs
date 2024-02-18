// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Forms;
using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Wpf.Ui.Controls;
using static FileActionsMenu.Helpers.FileActionProgressHelper;

namespace PowerToys.FileActionsMenu.Plugins.MoveCopyActions
{
    internal sealed class MoveTo : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Header => "Move to";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 1;

        public IconElement? Icon => new FontIcon { Glyph = "\uE8DE" };

        public bool IsVisible => true;

        public async Task Execute(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new()
            {
                AddToRecent = false,
                Description = "Move to",
                UseDescriptionForTitle = true,
                AutoUpgradeEnabled = true,
                ShowNewFolderButton = true,
                SelectedPath = Path.GetDirectoryName(SelectedItems[0]) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                FileActionProgressHelper fileActionProgressHelper = new("Moving files", SelectedItems.Length, () => { });

                int i = -1;
                foreach (string item in SelectedItems)
                {
                    i++;
                    fileActionProgressHelper.UpdateProgress(i, Path.GetFileName(item));

                    string destination = Path.Combine(dialog.SelectedPath, Path.GetFileName(item));
                    if (File.Exists(destination))
                    {
                        await fileActionProgressHelper.Conflict(item, () => File.Move(item, destination, true), () => { });
                    }
                    else
                    {
                        File.Move(item, destination);
                    }
                }
            }
        }
    }
}
