// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;
using CheckedMenuItemsDictionairy = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<(Wpf.Ui.Controls.MenuItem, FileActionsMenu.Ui.Actions.IAction)>>;

namespace FileActionsMenu.Ui.Actions.Hashes
{
    internal sealed class SHA3_256(Hashes.Hashes.HashCallingAction hashCallingAction) : IActionAndRequestCheckedMenuItems
    {
        private Hashes.Hashes.HashCallingAction _hashCallingAction = hashCallingAction;
        private string[]? _selectedItems;
        private CheckedMenuItemsDictionairy? _checkedMenuItemsDictionary;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public CheckedMenuItemsDictionairy CheckedMenuItemsDictionary { get => _checkedMenuItemsDictionary ?? throw new ArgumentNullException(nameof(CheckedMenuItemsDictionary)); set => _checkedMenuItemsDictionary = value; }

        public string Header => "SHA3-256";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public int Category => 0;

        public IconElement? Icon => null;

        public bool IsVisible => true;

        public IAction[]? SubMenuItems { get; }

        public async Task Execute(object sender, RoutedEventArgs e)
        {
            if (_hashCallingAction == Hashes.Hashes.HashCallingAction.GENERATE)
            {
                await Hashes.Hashes.GenerateHashes(Hashes.Hashes.HashType.SHA3_256, SelectedItems, CheckedMenuItemsDictionary);
            }
            else
            {
                await Hashes.Hashes.VerifyHashes(Hashes.Hashes.HashType.SHA3_256, SelectedItems, CheckedMenuItemsDictionary);
            }
        }
    }
}
