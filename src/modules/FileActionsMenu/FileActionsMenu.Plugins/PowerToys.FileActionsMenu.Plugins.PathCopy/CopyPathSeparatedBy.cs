// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Windows;
using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.PathCopy
{
    internal sealed class CopyPathSeparatedBy : IAction
    {
        private bool _isVisible;

        private string[]? _selectedItems;

        public string[] SelectedItems
        {
            get => _selectedItems.GetOrArgumentNullException();
            set
            {
                _selectedItems = value;

                if (value.Length > 1)
                {
                    _isVisible = true;
                }
            }
        }

        public string Title => ResourceHelper.GetResource("Path_Copy.CopyPathSeparatedBy.Title");

        public IAction.ItemType Type => IAction.ItemType.HasSubMenu;

        public IAction[]? SubMenuItems =>
        [
            new CopyPathSeparatedBySemicolon(),
            new CopyPathSeparatedBySpace(),
            new CopyPathSeparatedByComma(),
            new CopyPathSeparatedByNewline(),
            new CopyPathSeparatedByCustom()
        ];

        public int Category => 2;

        public IconElement? Icon => new FontIcon { Glyph = "\uF0E3" };

        public bool IsVisible => _isVisible;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            throw new InvalidOperationException();
        }

        public static void SeparateFilePathByDelimiterAndAddToClipboard(string delimiter, string[] items)
        {
            StringBuilder text = new();

            foreach (string filename in items)
            {
                text.Append(filename);
                text.Append(delimiter);
            }

            text.Length -= delimiter.Length;

            Clipboard.SetText(text.ToString());
        }
    }
}
