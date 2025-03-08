// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace PowerToys.FileActionsMenu.Plugins.Hashes
{
    internal sealed class InClipboard(Hashes.HashCallingAction hashCallingAction) : ICheckableAction
    {
        private readonly Hashes.HashCallingAction _hashCallingAction = hashCallingAction;
        private string[]? _selectedItems;

        public override string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public override string Title => _hashCallingAction == Hashes.HashCallingAction.GENERATE ? ResourceHelper.GetResource("Hashes.InClipboard.Generate.Title") : ResourceHelper.GetResource("Hashes.InClipboard.Verify.Title");

        public override IconElement? Icon => new FontIcon { Glyph = "\uf0e3" };

        public override bool IsVisible => SelectedItems.Length == 1;

        private bool _isChecked;

        public override bool IsChecked { get => _isChecked; set => _isChecked = value; }

        public override bool IsCheckedByDefault => false;

        public override string? CheckableGroupUUID => Hashes.GetUUID(_hashCallingAction);
    }
}
