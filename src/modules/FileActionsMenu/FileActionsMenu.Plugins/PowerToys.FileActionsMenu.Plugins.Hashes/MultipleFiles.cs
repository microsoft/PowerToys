// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace PowerToys.FileActionsMenu.Plugins.Hashes
{
    internal sealed class MultipleFiles(Hashes.HashCallingAction hashCallingAction) : ICheckableAction
    {
        private string[]? _selectedItems;

        public override string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public override string Title => hashCallingAction == Hashes.HashCallingAction.GENERATE ? ResourceHelper.GetResource("Hashes.MultipleFiles.Generate.Title") : ResourceHelper.GetResource("Hashes.MultipleFiles.Verify.Title");

        public override IconElement? Icon => new FontIcon { Glyph = "\ued43" };

        public override bool IsVisible => true;

        private bool _isChecked;

        public override bool IsChecked { get => _isChecked; set => _isChecked = value; }

        public override bool IsCheckedByDefault => false;

        public override string? CheckableGroupUUID => Hashes.GetUUID(hashCallingAction);
    }
}
