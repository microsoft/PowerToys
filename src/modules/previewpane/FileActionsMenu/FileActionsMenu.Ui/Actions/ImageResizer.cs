// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    internal sealed class ImageResizer : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Header => "Resize images with Image Resizer";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 3;

        public IconElement? Icon => new ImageIcon() { Source = Helpers.IconHelper.GetIconFromModuleName("ImageResizer"), Width = 10, Height = 10 };

        // Todo: Only visible if only Image Files are selected
        public bool IsVisible => true;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            StringBuilder arguments = new();

            foreach (string item in SelectedItems)
            {
                arguments.Append(CultureInfo.InvariantCulture, $"\"{item}\" ");
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = "PowerToys.ImageResizer.exe",
                Arguments = arguments.ToString(),
                UseShellExecute = true,
            };
            Process.Start(startInfo);
            return Task.CompletedTask;
        }
    }
}
