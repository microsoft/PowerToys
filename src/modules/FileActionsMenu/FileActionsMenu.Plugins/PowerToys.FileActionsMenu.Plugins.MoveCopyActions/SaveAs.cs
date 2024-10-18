// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Forms;
using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PowerToys.FileActionsMenu.Plugins.MoveCopyActions
{
    internal sealed class SaveAs : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("Move_Copy_Actions.SaveAs.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 1;

        public IconElement? Icon => new FontIcon { Glyph = "\uE792" };

        public bool IsVisible => SelectedItems.Length == 1 && !Directory.Exists(SelectedItems[0]);

        public async Task Execute(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                AddToRecent = false,
                CheckPathExists = true,
                CheckWriteAccess = true,
                FileName = Path.GetFileName(SelectedItems[0]),
                InitialDirectory = Path.GetDirectoryName(SelectedItems[0]) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                OverwritePrompt = false,
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                FileActionProgressHelper fileActionProgressHelper = new(ResourceHelper.GetResource("Move_Copy_Actions.SaveAs.Title"), 1, () => { });

                fileActionProgressHelper.UpdateProgress(0, Path.GetFileName(SelectedItems[0]));

                if (File.Exists(dialog.FileName))
                {
                    await fileActionProgressHelper.Conflict(dialog.FileName, () => File.Move(SelectedItems[0], dialog.FileName, true), () => { });
                }
                else
                {
                    File.Move(SelectedItems[0], dialog.FileName);
                }

                dialog.Dispose();
            }
        }
    }
}
