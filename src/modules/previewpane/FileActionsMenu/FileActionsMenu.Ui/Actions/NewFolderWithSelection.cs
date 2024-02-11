// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    internal sealed class NewFolderWithSelection : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Header => "New folder with selection";

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 1;

        public IconElement? Icon => new FontIcon { Glyph = "\uE8F4" };

        public bool IsVisible => true;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(Path.GetDirectoryName(SelectedItems[0]) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "New folder with selection");

            int i = 0;
            while (Directory.Exists(path))
            {
                if (path.EndsWith(')'))
                {
                    path = path[..^(3 + i.ToString(CultureInfo.InvariantCulture).Length)];
                }

                i++;
                path += " (" + i + ")";
            }

            Directory.CreateDirectory(path);

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

            return Task.CompletedTask;
        }
    }
}
