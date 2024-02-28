// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.FileContentActions
{
    internal sealed class MergeContent : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Header => "Merge files";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 2;

        public IconElement? Icon => new FontIcon() { Glyph = "\uea3c" };

        public bool IsVisible => SelectedItems.Length > 1 && !SelectedItems.Any(Directory.Exists);

        public async Task Execute(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new();
            saveFileDialog.Filter = "All files|*.*";
            saveFileDialog.Title = "Save merged file";
            saveFileDialog.DefaultExt = ".*";
            saveFileDialog.InitialDirectory = Path.GetDirectoryName(SelectedItems[0]);
            DialogResult result = saveFileDialog.ShowDialog();

            if (result != DialogResult.OK)
            {
                return;
            }

            Stream fs = saveFileDialog.OpenFile();
            foreach (var item in SelectedItems)
            {
                using Stream source = File.OpenRead(item);
                await source.CopyToAsync(fs);
            }

            fs.Close();
        }
    }
}
