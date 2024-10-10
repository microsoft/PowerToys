// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Web;
using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using FontFamily = Microsoft.UI.Xaml.Media.FontFamily;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.FileContentActions
{
    internal sealed class AsURIEncoded : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("File_Content_Actions.CopyContentAsUriEncoded.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 0;

        public IconElement? Icon => new FontIcon { Glyph = "URI", FontFamily = FontFamily.XamlAutoFontFamily };

        public bool IsVisible => SelectedItems.Length == 1 && !Directory.Exists(SelectedItems[0]);

        public Task Execute(object sender, RoutedEventArgs e)
        {
            TelemetryHelper.LogEvent(new FileActionsMenuCopyContentAsUriEncodedActionInvokedEvent(), SelectedItems);

            byte[] fileContent = File.ReadAllBytes(SelectedItems[0]);

            System.Windows.Clipboard.SetText(HttpUtility.UrlEncode(fileContent));
            return Task.CompletedTask;
        }
    }
}
