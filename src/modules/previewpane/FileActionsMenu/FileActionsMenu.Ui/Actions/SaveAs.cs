// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    internal sealed class SaveAs : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Header => "Save file as";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 1;

        public IconElement? Icon => new FontIcon { Glyph = "\uE792" };

        public bool IsVisible => SelectedItems.Length == 1;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                AddToRecent = false,
                CheckPathExists = true,
                CheckWriteAccess = true,
                FileName = Path.GetFileName(SelectedItems[0]),
                InitialDirectory = Path.GetDirectoryName(SelectedItems[0]) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                OverwritePrompt = true,
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                File.Move(SelectedItems[0], dialog.FileName);

                dialog.Dispose();
            }

            return Task.CompletedTask;
        }
    }
}
