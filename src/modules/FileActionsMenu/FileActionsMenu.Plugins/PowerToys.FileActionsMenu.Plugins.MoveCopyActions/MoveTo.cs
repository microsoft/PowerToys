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
    internal sealed class MoveTo : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("Move_Copy_Actions.MoveTo.Title");

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
                Description = ResourceHelper.GetResource("Move_Copy_Actions.MoveTo.Title"),
                UseDescriptionForTitle = true,
                AutoUpgradeEnabled = true,
                ShowNewFolderButton = true,
                SelectedPath = Path.GetDirectoryName(SelectedItems[0]) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                bool cancelled = false;
                FileActionProgressHelper fileActionProgressHelper = new(ResourceHelper.GetResource("Move_Copy_Actions.MoveTo.Progress"), SelectedItems.Length, () => { cancelled = true; });

                int i = -1;
                foreach (string item in SelectedItems)
                {
                    if (cancelled)
                    {
                        return;
                    }

                    i++;

                    if (File.Exists(item))
                    {
                        fileActionProgressHelper.UpdateProgress(i, Path.GetFileName(item));
                        string destination = Path.Combine(dialog.SelectedPath, Path.GetFileName(item));

                        if (item == destination)
                        {
                            continue;
                        }

                        if (File.Exists(destination))
                        {
                            await fileActionProgressHelper.Conflict(destination, () => File.Move(item, destination, true), () => { });
                        }
                        else
                        {
                            File.Move(item, destination);
                        }
                    }
                    else if (Directory.Exists(item))
                    {
                        fileActionProgressHelper.UpdateProgress(i, Path.GetFileName(item));
                        string destination = Path.Combine(dialog.SelectedPath, Path.GetFileName(item));

                        if (item == destination)
                        {
                            continue;
                        }

                        if (Directory.Exists(destination))
                        {
                            await fileActionProgressHelper.Conflict(
                                item,
                                () =>
                                {
                                    Directory.Delete(destination, true);
                                    Directory.Move(item, destination);
                                },
                                () => { });
                        }
                        else
                        {
                            Directory.Move(item, destination);
                        }
                    }
                }
            }
        }
    }
}
