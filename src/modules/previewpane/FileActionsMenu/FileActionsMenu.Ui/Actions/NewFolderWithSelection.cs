// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Windows;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    internal sealed class NewFolderWithSelection : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Header => "New folder with selection";

        public bool HasSubMenu => false;

        public IAction[]? SubMenuItems => null;

        public int Category => 1;

        public IconElement? Icon => new FontIcon { Glyph = "&#xE8DE;" };

        public bool IsVisible => true;

        public void Execute(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(SelectedItems[0]) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "New folder with selection"));

            CancellationTokenSource cancellationTokenSource = new() { };
            CopyMoveUi copyMoveUi = new("Moving", SelectedItems.Length, cancellationTokenSource);

            foreach (string item in SelectedItems)
            {
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    copyMoveUi.Close();
                    break;
                }

                copyMoveUi.CurrentFile = Path.GetFileName(item);

                File.Move(item, Path.Combine(Path.GetDirectoryName(SelectedItems[0]) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "New folder with selection", Path.GetFileName(item)));
                copyMoveUi.Progress++;
            }
        }
    }
}
