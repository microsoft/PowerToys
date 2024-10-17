// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileActionsMenu.Ui
{
    /// <summary>
    /// Action that does nothing, so it just closes the menu.
    /// </summary>
    internal sealed class CloseAction : IAction
    {
        public string[] SelectedItems { get => []; set => _ = value; }

        public string Title => ResourceHelper.GetResource("Close");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 99;

        public IconElement? Icon => new FontIcon() { Glyph = "\uE8BB" };

        public bool IsVisible => true;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            return Task.CompletedTask;
        }
    }
}
