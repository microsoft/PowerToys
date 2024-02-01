// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    internal interface IAction
    {
        public string[] SelectedItems { get; set; }

        public string Header { get; }

        public bool HasSubMenu { get; }

        public IAction[]? SubMenuItems { get; }

        public int Category { get; }

        public IconElement? Icon { get; }

        public bool IsVisible { get; }

        public void Execute(object sender, RoutedEventArgs e);
    }
}
