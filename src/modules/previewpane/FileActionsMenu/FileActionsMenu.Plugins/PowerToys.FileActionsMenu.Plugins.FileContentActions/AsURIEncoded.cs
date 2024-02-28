// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Web;
using System.Windows;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using FontFamily = Microsoft.UI.Xaml.Media.FontFamily;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.FileContentActions
{
    internal sealed class AsURIEncoded : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Header => "As URI encoded string";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 0;

        public IconElement? Icon => new FontIcon { Glyph = "URI", FontFamily = FontFamily.XamlAutoFontFamily };

        public bool IsVisible => SelectedItems.Length == 1 && !Directory.Exists(SelectedItems[0]);

        public Task Execute(object sender, RoutedEventArgs e)
        {
            byte[] fileContent = File.ReadAllBytes(SelectedItems[0]);

            System.Windows.Clipboard.SetText(HttpUtility.UrlEncode(fileContent));
            return Task.CompletedTask;
        }
    }
}
