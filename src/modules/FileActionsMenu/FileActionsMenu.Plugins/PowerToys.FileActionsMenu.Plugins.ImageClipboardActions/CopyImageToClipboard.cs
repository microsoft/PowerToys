// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.ImageClipboardActions
{
    internal sealed class CopyImageToClipboard : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("Image_Clipboard_Actions.CopyToClipboard.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 4;

        public IconElement? Icon => new FontIcon { Glyph = "\ue8e5" };

        public bool IsVisible => SelectedItems.Length == 1 && SelectedItems[0].IsImage();

        public Task Execute(object sender, RoutedEventArgs e)
        {
            TelemetryHelper.LogEvent(new FileActionsMenuCopyImageToClipboardActionInvokedEvent(), SelectedItems);

            Clipboard.SetImage(new System.Windows.Media.Imaging.BitmapImage(new Uri(SelectedItems[0])));
            return Task.CompletedTask;
        }
    }
}
