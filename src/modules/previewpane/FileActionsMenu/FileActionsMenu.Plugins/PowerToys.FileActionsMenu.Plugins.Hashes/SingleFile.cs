// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using FileActionsMenu.Helpers;
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

#pragma warning disable CA1863 // Use 'CompositeFormat'
        public override string Title => _hashCallingAction == Hashes.HashCallingAction.GENERATE ? ResourceHelper.GetResource("Hashes.SingleFile.Generate.Title") : string.Format(CultureInfo.InvariantCulture, ResourceHelper.GetResource("Hashes.SingleFile.Verify.Title"), ResourceHelper.GetResource("Hashes.Generate.Filename"));
#pragma warning restore CA1863 // Use 'CompositeFormat'

        public override IconElement? Icon => new FontIcon { Glyph = "\ue8a5" };

        public override bool IsVisible => true;

        private bool _isChecked;

        public override bool IsChecked { get => _isChecked; set => _isChecked = value; }

        public override bool IsCheckedByDefault => true;

        public override string? CheckableGroupUUID => Hashes.GetUUID(_hashCallingAction);
    }
}
