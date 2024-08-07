// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PowerToys.FileActionsMenu.Plugins.Hashes
{
    internal sealed class MD5(Hashes.HashCallingAction hashCallingAction) : IActionAndRequestCheckedMenuItems
    {
        private readonly Hashes.HashCallingAction _hashCallingAction = hashCallingAction;
        private string[]? _selectedItems;
        private CheckedMenuItemsDictionary? _checkedMenuItemsDictionary;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public CheckedMenuItemsDictionary CheckedMenuItemsDictionary { get => _checkedMenuItemsDictionary.GetOrArgumentNullException(); set => _checkedMenuItemsDictionary = value; }

        public string Title => "MD5";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public int Category => 0;

        public IconElement? Icon => new FontIcon { Glyph = "MD5", FontFamily = FontFamily.XamlAutoFontFamily };

        public bool IsVisible => true;

        public IAction[]? SubMenuItems { get; }

        public async Task Execute(object sender, RoutedEventArgs e)
        {
            if (_hashCallingAction == Hashes.HashCallingAction.GENERATE)
            {
                await Hashes.GenerateHashes(HashEnums.HashType.MD5, SelectedItems, CheckedMenuItemsDictionary);
            }
            else
            {
                await Hashes.VerifyHashes(HashEnums.HashType.MD5, SelectedItems, CheckedMenuItemsDictionary);
            }
        }
    }
}
