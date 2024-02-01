// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions.Hashes
{
    internal sealed class Sha1 : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Header => "Sha1 hash";

        public bool HasSubMenu => false;

        public int Category => 0;

        public IconElement? Icon => null;

        public bool IsVisible => true;

        public IAction[]? SubMenuItems { get; }

        public void Execute(object sender, RoutedEventArgs e)
        {
            Hashes.Hashes.GenerateHashes(Hashes.Hashes.HashType.Sha1, SelectedItems);
        }
    }
}
