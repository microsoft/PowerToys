// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using FontFamily = Microsoft.UI.Xaml.Media.FontFamily;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.FileContentActions
{
    internal sealed class AsCString : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("File_Content_Actions.CopyContentAsCString.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 0;

        public IconElement? Icon => new FontIcon { Glyph = "C", FontFamily = FontFamily.XamlAutoFontFamily };

        public bool IsVisible => SelectedItems.Length == 1 && !Directory.Exists(SelectedItems[0]);

        public Task Execute(object sender, RoutedEventArgs e)
        {
            TelemetryHelper.LogEvent(new FileActionsMenuCopyContentAsCEscapedStringActionInvokedEvent(), SelectedItems);

            Dictionary<string, string> escapeSequences = new()
            {
                ["\x5C"] = "\\\\",
                ["\x07"] = "\\a",
                ["\x08"] = "\\b",
                ["\x09"] = "\\t",
                ["\x0A"] = "\\n",
                ["\x0B"] = "\\v",
                ["\x0C"] = "\\f",
                ["\x0D"] = "\\r",
                ["\x22"] = "\\\"",
                ["\x27"] = "\\'",
            };

            string fileContent = File.ReadAllText(SelectedItems[0]);

            foreach (var escapeSequence in escapeSequences)
            {
                fileContent = fileContent.Replace(escapeSequence.Key, escapeSequence.Value);
            }

            System.Windows.Clipboard.SetText(fileContent);
            return Task.CompletedTask;
        }
    }
}
