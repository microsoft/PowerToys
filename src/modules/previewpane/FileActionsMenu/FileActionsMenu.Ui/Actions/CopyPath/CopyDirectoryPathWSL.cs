// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions.CopyPath
{
    internal sealed class CopyDirectoryPathWSL : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Header => "Copy containing directory path for WSL";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 0;

        public IconElement? Icon => null;

        public bool IsVisible => true;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            string tmpPath;

            if (Directory.Exists(SelectedItems[0]))
            {
                tmpPath = Directory.GetParent(SelectedItems[0])?.FullName ?? string.Empty;
            }
            else
            {
                tmpPath = Path.GetDirectoryName(SelectedItems[0]) ?? string.Empty;
            }

            Clipboard.SetText("/mnt/" + tmpPath[0].ToString().ToLowerInvariant() + tmpPath[1..].Replace("\\", "/").Replace(":/", "/"));
            return Task.CompletedTask;
        }
    }
}
