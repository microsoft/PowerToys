// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileActionsMenu.Interfaces
{
    /// <summary>
    /// A separator in the menu.
    /// </summary>
    public sealed class Separator : IAction
    {
        public string[] SelectedItems { get => []; set => _ = value; }

        public string Title => string.Empty;

        public IAction.ItemType Type => IAction.ItemType.Separator;

        public IAction[]? SubMenuItems => null;

        public int Category => 0;

        public IconElement? Icon => null;

        public bool IsVisible => true;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            throw new InvalidOperationException();
        }
    }
}
