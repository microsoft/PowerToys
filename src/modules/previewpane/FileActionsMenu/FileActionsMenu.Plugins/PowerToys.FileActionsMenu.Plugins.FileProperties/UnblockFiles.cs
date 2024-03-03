// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PowerToys.FileActionsMenu.Plugins.FileProperties
{
    internal sealed class UnblockFiles : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("File_Properties.Unblock.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 8;

        public IconElement? Icon => new FontIcon { Glyph = "\ue785" };

        public bool IsVisible => SelectedItems.Any(file => File.Exists(file + ":Zone.Identifier"));

        public Task Execute(object sender, RoutedEventArgs e)
        {
            TelemetryHelper.LogEvent(new FileActionsMenuUnblockFilesActionInvokedEvent(), SelectedItems);

            foreach (string file in SelectedItems)
            {
                if (!File.Exists(file + ":Zone.Identifier"))
                {
                    continue;
                }

                File.Delete(file + ":Zone.Identifier");
            }

            return Task.CompletedTask;
        }
    }
}
