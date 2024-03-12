// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.PathCopy
{
    internal sealed class CopyPath : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems
        {
            get => _selectedItems.GetOrArgumentNullException();
            set => _selectedItems = value;
        }

        public string Title => ResourceHelper.GetResource("Path_Copy.CopyPath.Title");

        public IAction.ItemType Type => IAction.ItemType.HasSubMenu;

        public IAction[]? SubMenuItems
        {
            get
            {
                IAction[] items = [
                    new CopyFileName(),
                    new CopyDirectoryPath(),
                    new CopyFullPathBackSlash(),
                    new CopyFullPathDoubleBackSlash(),
                    new CopyFullPathForwardSlash(),
                    new CopyFullPathWSL(),
                    new CopyDirectoryPathWSL(),
                ];

                if (SelectedItems.Length == 1 && SelectedItems[0].EndsWith(".lnk", StringComparison.InvariantCultureIgnoreCase) && !Directory.Exists(SelectedItems[0]))
                {
                    items = [new ResolveShortcut(), new HandleShortcut(), new Separator(), .. items];
                }

                return items;
            }
        }

        public int Category => 2;

        public IconElement? Icon => new FontIcon { Glyph = "\uF0E3" };

        public bool IsVisible => SelectedItems.Length == 1;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            throw new InvalidOperationException();
        }
    }
}
