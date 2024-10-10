// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Security;
using System.Text.Json;
using System.Windows;
using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using FontFamily = Microsoft.UI.Xaml.Media.FontFamily;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.FileContentActions
{
    internal sealed class AsXmlEncoded : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("File_Content_Actions.CopyContentAsXmlEncoded.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 0;

        public IconElement? Icon => new FontIcon { Glyph = "XML", FontFamily = FontFamily.XamlAutoFontFamily };

        public bool IsVisible => SelectedItems.Length == 1 && !Directory.Exists(SelectedItems[0]);

        public Task Execute(object sender, RoutedEventArgs e)
        {
            TelemetryHelper.LogEvent(new FileActionsMenuCopyContentAsXmlEncodedActionInvokedEvent(), SelectedItems);

            string fileContent = File.ReadAllText(SelectedItems[0]);

            fileContent = SecurityElement.Escape(fileContent);

            System.Windows.Clipboard.SetText(fileContent);
            return Task.CompletedTask;
        }
    }
}
