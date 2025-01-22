// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace PowerToys.FileActionsMenu.Plugins.PathCopy
{
    internal sealed class HandleShortcut : ICheckableAction
    {
        private string[]? _selectedItems;
        private bool _isChecked;

        public override string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public override string Title => ResourceHelper.GetResource("Path_Copy.HandleShortcut.Title");

        public override IconElement? Icon => null;

        public override bool IsVisible => SelectedItems.Length == 1 && SelectedItems[0].EndsWith(".lnk", StringComparison.InvariantCultureIgnoreCase);

        public override bool IsChecked { get => _isChecked; set => _isChecked = value; }

        public override bool IsCheckedByDefault => false;

        public override string? CheckableGroupUUID => "f2544fd5-13f7-4d52-b7b4-00a3c70923e6";
    }
}
