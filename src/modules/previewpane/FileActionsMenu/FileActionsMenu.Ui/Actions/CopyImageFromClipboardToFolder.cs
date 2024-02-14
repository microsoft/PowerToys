// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    internal sealed class CopyImageFromClipboardToFolder : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Header => "Copy image from clipboard into Folder";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 4;

        public IconElement? Icon => null;

        public bool IsVisible => SelectedItems.Length == 1 && Directory.Exists(SelectedItems[0]) && Clipboard.ContainsImage();

        public Task Execute(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(SelectedItems[0]) || !Clipboard.ContainsImage())
            {
                return Task.CompletedTask;
            }

            string path = Path.Combine(SelectedItems[0], "clipboard_image.png");
            int i = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(SelectedItems[0], $"clipboard_image ({i}).png");
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
