// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace PowerToys.FileActionsMenu.Plugins.Hashes
{
    internal sealed class SingleFile(Hashes.HashCallingAction hashCallingAction) : ICheckableAction
    {
        private readonly Hashes.HashCallingAction _hashCallingAction = hashCallingAction;
        private string[]? _selectedItems;

        public override string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public override string Header => _hashCallingAction == Hashes.HashCallingAction.GENERATE ? "Save hashes in single file" : "Compare with hashes in file called \"Hashes\"";

        public override IconElement? Icon => new FontIcon { Glyph = "\ue8a5" };

        public override bool IsVisible => true;

        private bool _isChecked;

        public override bool IsChecked { get => _isChecked; set => _isChecked = value; }

        public override bool IsCheckedByDefault => true;

        public override string? CheckableGroupUUID => Hashes.GetUUID(_hashCallingAction);
    }
}
