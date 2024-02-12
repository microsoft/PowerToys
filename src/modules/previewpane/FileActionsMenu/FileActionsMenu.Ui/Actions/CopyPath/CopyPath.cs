// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions.CopyPath
{
    internal sealed class CopyPath : IAction
    {
        private bool _isVisibile;

        public string[] SelectedItems
        {
            get => [];
            set
            {
                if (value.Length > 0)
                {
                    _isVisibile = true;
                }
            }
        }

        public string Header => "Copy part of path";

        public IAction.ItemType Type => IAction.ItemType.HasSubMenu;

        public IAction[]? SubMenuItems =>
        [
            new CopyFileName(),
            new CopyDirectoryPath(),
            new CopyFullPathBackSlash(),
            new CopyFullPathDoubleBackSlash(),
            new CopyFullPathForwardSlash(),
            new CopyFullPathWSL(),
            new CopyDirectoryPathWSL(),
        ];

        public int Category => 2;

        public IconElement? Icon => new FontIcon { Glyph = "\uF0E3" };

        public bool IsVisible => _isVisibile;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            throw new InvalidOperationException();
        }
    }
}
