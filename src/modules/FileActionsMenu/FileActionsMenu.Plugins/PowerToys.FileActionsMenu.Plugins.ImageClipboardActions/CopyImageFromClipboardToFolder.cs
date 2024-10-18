// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Media.Imaging;
using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.ImageClipboardActions
{
    internal sealed class CopyImageFromClipboardToFolder : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("Image_Clipboard_Actions.CopyFromClipboard.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 4;

        public IconElement? Icon => new FontIcon { Glyph = "\ue8de" };

        public bool IsVisible => SelectedItems.Length == 1 && Directory.Exists(SelectedItems[0]) && Clipboard.ContainsImage();

        public Task Execute(object sender, RoutedEventArgs e)
        {
            TelemetryHelper.LogEvent(new FileActionsMenuCopyImageFromClipboardActionInvokedEvent(), SelectedItems);

            if (!Directory.Exists(SelectedItems[0]) || !Clipboard.ContainsImage())
            {
                return Task.CompletedTask;
            }

            string fileName = ResourceHelper.GetResource("Image_Clipboard_Actions.CopyFromClipboard.FileName");
            string path = Path.Combine(SelectedItems[0], $"{fileName}.png");
            int i = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(SelectedItems[0], $"{fileName} ({i}).png");
                i++;
            }

            BitmapSource source = Clipboard.GetImage();
            using var fileStream = new FileStream(path, FileMode.Create);
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(fileStream);
            return Task.CompletedTask;
        }
    }
}
