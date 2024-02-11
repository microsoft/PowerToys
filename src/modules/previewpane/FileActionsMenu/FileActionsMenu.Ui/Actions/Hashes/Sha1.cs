// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;
using CheckedMenuItemsDictionairy = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<(Wpf.Ui.Controls.MenuItem, FileActionsMenu.Ui.Actions.IAction)>>;

namespace FileActionsMenu.Ui.Actions.Hashes
{
    internal sealed class Sha1 : IActionAndRequestCheckedMenuItems
    {
        private string[]? _selectedItems;
        private CheckedMenuItemsDictionairy? _checkedMenuItemsDictionary;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public CheckedMenuItemsDictionairy CheckedMenuItemsDictionary { get => _checkedMenuItemsDictionary ?? throw new ArgumentNullException(nameof(CheckedMenuItemsDictionary)); set => _checkedMenuItemsDictionary = value; }

        public string Header => "Sha1 hash";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public int Category => 0;

        public IconElement? Icon => null;

        public bool IsVisible => true;

        public IAction[]? SubMenuItems { get; }

        public async Task Execute(object sender, RoutedEventArgs e)
        {
            await Hashes.Hashes.GenerateHashes(Hashes.Hashes.HashType.Sha1, SelectedItems, CheckedMenuItemsDictionary);
        }
    }
}
