// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using System.Windows;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions.Hashes
{
    internal sealed class SHA512(Hashes.Hashes.HashCallingAction hashCallingAction) : IActionAndRequestCheckedMenuItems
    {
        private readonly Hashes.Hashes.HashCallingAction _hashCallingAction = hashCallingAction;
        private string[]? _selectedItems;
        private CheckedMenuItemsDictionary? _checkedMenuItemsDictionary;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public CheckedMenuItemsDictionary CheckedMenuItemsDictionary { get => _checkedMenuItemsDictionary.GetOrArgumentNullException(); set => _checkedMenuItemsDictionary = value; }

        public string Header => "SHA512";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public int Category => 0;

        public IconElement? Icon => null;

        public bool IsVisible => true;

        public IAction[]? SubMenuItems { get; }

        public async Task Execute(object sender, RoutedEventArgs e)
        {
            if (_hashCallingAction == Hashes.Hashes.HashCallingAction.GENERATE)
            {
                await Hashes.Hashes.GenerateHashes(Hashes.Hashes.HashType.SHA512, SelectedItems, CheckedMenuItemsDictionary);
            }
            else
            {
                await Hashes.Hashes.VerifyHashes(Hashes.Hashes.HashType.SHA512, SelectedItems, CheckedMenuItemsDictionary);
            }
        }
    }
}
