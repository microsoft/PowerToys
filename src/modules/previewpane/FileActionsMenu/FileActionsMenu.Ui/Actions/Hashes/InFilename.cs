// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions.Hashes
{
    internal sealed class InFilename(Hashes.Hashes.HashCallingAction hashCallingAction) : ICheckableAction
    {
        private Hashes.Hashes.HashCallingAction _hashCallingAction = hashCallingAction;
        private string[]? _selectedItems;

        public override string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public override string Header => _hashCallingAction == Hashes.Hashes.HashCallingAction.GENERATE ? "Replace filename with hash" : "Compare content with filename";

        public override IconElement? Icon => null;

        public override bool IsVisible => true;

        private bool _isChecked;

        public override bool IsChecked { get => _isChecked; set => _isChecked = value; }

        public override bool IsCheckedByDefault => false;

        public override string? CheckableGroupUUID => "2a89265d-a55a-4a48-b35f-a48f3e8bc2ea";
    }
}
