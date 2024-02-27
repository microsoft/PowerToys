// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PowerToys.FileActionsMenu.Plugins.FileContentActions
{
    internal sealed class CopyFileContent : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Header => "Copy content of file";

        public IAction.ItemType Type => IAction.ItemType.HasSubMenu;

        public IAction[]? SubMenuItems =>
        [
            new AsPlaintext(),
        ];

        public int Category => 2;

        public IconElement? Icon => new FontIcon() { Glyph = "\ue8c8" };

        public bool IsVisible => SelectedItems.Length == 1;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            throw new InvalidOperationException();
        }
    }
}
