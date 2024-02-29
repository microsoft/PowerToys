// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PowerToys.FileActionsMenu.Plugins.Hashes
{
    internal sealed class CRC32(Hashes.HashCallingAction hashCallingAction) : IAction
    {
        private readonly Hashes.HashCallingAction _hashCallingAction = hashCallingAction;

        public string[] SelectedItems { get => []; set => _ = value; }

        public string Title => "CRC32";

        public IAction.ItemType Type => IAction.ItemType.HasSubMenu;

        public int Category => 0;

        public IconElement? Icon => new FontIcon { Glyph = "CRC", FontFamily = FontFamily.XamlAutoFontFamily };

        public bool IsVisible => true;

        public IAction[]? SubMenuItems =>
        [
            new CRC32Hex(_hashCallingAction),
            new CRC32Decimal(_hashCallingAction),
        ];

        public Task Execute(object sender, RoutedEventArgs e)
        {
            throw new InvalidOperationException();
        }
    }
}
