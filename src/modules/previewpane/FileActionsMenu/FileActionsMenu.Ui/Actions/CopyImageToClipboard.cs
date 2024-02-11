// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    internal sealed class CopyImageToClipboard : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Header => "Copy image to clipboard";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 4;

        public IconElement? Icon => null;

        // Todo: Only if image file is selected
        public bool IsVisible => SelectedItems.Length == 1;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            Clipboard.SetImage(new System.Windows.Media.Imaging.BitmapImage(new Uri(SelectedItems[0])));
            return Task.CompletedTask;
        }
    }
}
