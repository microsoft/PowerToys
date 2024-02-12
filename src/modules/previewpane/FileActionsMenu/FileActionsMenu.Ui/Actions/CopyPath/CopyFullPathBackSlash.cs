// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions.CopyPath
{
    internal sealed class CopyFullPathBackSlash : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Header => "Copy full path (\\)";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 0;

        public IconElement? Icon => null;

        public bool IsVisible => true;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(SelectedItems[0]);
            return Task.CompletedTask;
        }
    }
}
