// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    internal sealed class ImageResizer : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Header => "Resize images with Image Resizer";

        public bool HasSubMenu => false;

        public IAction[]? SubMenuItems => null;

        public int Category => 3;

        public IconElement? Icon => null;

        // Todo: Only visible if only Image Files are selected
        public bool IsVisible => true;

        public void Execute(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
