// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    internal sealed class MoveTo : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Header => "Move to";

        public bool HasSubMenu => false;

        public IAction[]? SubMenuItems => null;

        public int Category => 1;

        public IconElement? Icon => new FontIcon { Glyph = "&#xE8C6;" };

        public bool IsVisible => true;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new()
            {
                AddToRecent = false,
                Description = "Copy to",
                UseDescriptionForTitle = true,
                AutoUpgradeEnabled = true,
                ShowNewFolderButton = true,
                SelectedPath = Path.GetDirectoryName(SelectedItems[0]) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                CancellationTokenSource cancellationTokenSource = new() { };

                CopyMoveUi copyMoveUi = new("Copying", SelectedItems.Length, cancellationTokenSource);

                copyMoveUi.Show();

                foreach (string item in SelectedItems)
                {
                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        copyMoveUi.Close();
                        break;
                    }

                    copyMoveUi.CurrentFile = Path.GetFileName(item);

                    string destination = Path.Combine(dialog.SelectedPath, Path.GetFileName(item));
                    if (File.Exists(destination))
                    {
                        CopyMoveConflictUi conflictUi = new(
                            Path.GetFileName(destination),
                            () =>
                            {
                                File.Copy(item, destination, true);
                                copyMoveUi.Progress++;
                            },
                            () =>
                            {
                                copyMoveUi.Progress++;
                            }
                        );
                        conflictUi.ShowDialog();
                        continue;
                    }

                    File.Copy(item, destination);
                    copyMoveUi.Progress++;
                }

                dialog.Dispose();
                copyMoveUi.Close();
            }

            return Task.CompletedTask;
        }
    }
}
