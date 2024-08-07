// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.FileContentActions
{
    internal sealed class CollapseFolderStructure : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("File_Content_Actions.CollapseFolder.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 2;

        public IconElement? Icon => new FontIcon() { Glyph = "\uea3c" };

        public bool IsVisible => SelectedItems.Length == 1 && Directory.Exists(SelectedItems[0]);

        public async Task Execute(object sender, RoutedEventArgs e)
        {
            bool cancelled = false;
            int numberOfFiles = CountFilesInDirectory(SelectedItems[0]);

            TelemetryHelper.LogEvent(new FileActionsMenuCollapseFolderActionInvokedEvent() { CollapsedFilesCount = numberOfFiles }, SelectedItems);

            FileActionProgressHelper fileActionProgressHelper = new(ResourceHelper.GetResource("File_Content_Actions.CollapseFolder.Title"), numberOfFiles, () => { cancelled = true; });

            int i = 0;
            foreach (string file in Directory.EnumerateFiles(SelectedItems[0], "*", SearchOption.AllDirectories))
            {
                if (cancelled)
                {
                    break;
                }

                fileActionProgressHelper.UpdateProgress(i, Path.GetFileName(file));
                i++;

                if (Path.GetDirectoryName(file) == SelectedItems[0])
                {
                    continue;
                }

                if (File.Exists(Path.Combine(SelectedItems[0], Path.GetFileName(file))))
                {
                    await fileActionProgressHelper.Conflict(file, () => { File.Move(file, Path.Combine(SelectedItems[0], Path.GetFileName(file)), true); }, () => { });
                }
                else
                {
                    File.Move(file, Path.Combine(SelectedItems[0], Path.GetFileName(file)));
                }
            }

            foreach (string directory in Directory.GetDirectories(SelectedItems[0]))
            {
                if (CountFilesInDirectory(directory) == 0)
                {
                    Directory.Delete(directory);
                }
            }
        }

        private int CountFilesInDirectory(string directory)
        {
            int count = 0;
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                count++;
            }

            return count;
        }
    }
}
