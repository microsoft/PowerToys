// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Ui.Helpers;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions.Hashes
{
    internal sealed class InFilename(Hashes.Hashes.HashCallingAction hashCallingAction) : ICheckableAction
    {
        private readonly Hashes.Hashes.HashCallingAction _hashCallingAction = hashCallingAction;
        private string[]? _selectedItems;

        public override string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public override string Header => _hashCallingAction == Hashes.Hashes.HashCallingAction.GENERATE ? "Replace filename with hash" : "Compare content with filename";

        public override IconElement? Icon => null;

        public override bool IsVisible => true;

        private bool _isChecked;

        public override bool IsChecked { get => _isChecked; set => _isChecked = value; }

        public override bool IsCheckedByDefault => false;

        public override string? CheckableGroupUUID => Hashes.Hashes.GetUUID(_hashCallingAction);
    }
}
