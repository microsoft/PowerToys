// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    internal sealed class Close : IAction
    {
        public string[] SelectedItems { get => []; set => _ = value; }

        public string Header => "Close menu";

        public IAction.ItemType Type => IAction.ItemType.Single;

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
