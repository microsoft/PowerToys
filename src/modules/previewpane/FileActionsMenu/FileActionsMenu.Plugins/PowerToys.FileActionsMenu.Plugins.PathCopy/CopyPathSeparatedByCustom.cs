// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Windows.Forms;
using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using Microsoft.UI.Xaml.Controls;
using Button = System.Windows.Forms.Button;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;
using TextBox = System.Windows.Forms.TextBox;

namespace PowerToys.FileActionsMenu.Plugins.PathCopy
{
    internal sealed class CopyPathSeparatedByCustom : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("Path_Copy.CopyPathSeparatedBy.Custom.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 0;

        public IconElement? Icon => null;

        public bool IsVisible => true;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            TextBox customDelimiterTextBox = new() { Margin = new Padding(2), Location = new Point(0, 0) };
            Button okButton = new() { Text = ResourceHelper.GetResource("Path_Copy.CopyPathSeparatedBy.Custom.Dialog.Ok"), Location = new Point(0, 25), Margin = new Padding(2) };
            Button cancelButton = new() { Text = ResourceHelper.GetResource("Path_Copy.CopyPathSeparatedBy.Custom.Dialog.Cancel"), Margin = new Padding(2), Location = new Point(okButton.Width, 25) };
            Form window = new()
            {
                Text = ResourceHelper.GetResource("Path_Copy.CopyPathSeparatedBy.Custom.Dialog.Title"),
                AcceptButton = okButton,
                CancelButton = cancelButton,
                Height = okButton.Height + customDelimiterTextBox.Height + 6,
                Width = okButton.Width + cancelButton.Width + 18,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
            };
            okButton.Click += (sender, e) =>
            {
                TelemetryHelper.LogEvent(new FileActionsMenuCopyFilePathsSeparatedByActionInvokedEvent() { Delimiter = customDelimiterTextBox.Text, IsCustomSeparator = true }, SelectedItems);

                CopyPathSeparatedBy.SeparateFilePathByDelimiterAndAddToClipboard(customDelimiterTextBox.Text, SelectedItems);
                window.Close();
            };
            cancelButton.Click += (sender, e) => { window.Close(); };

            window.Controls.Add(customDelimiterTextBox);
            window.Controls.Add(okButton);
            window.Controls.Add(cancelButton);

            window.Anchor = AnchorStyles.None;

            Rectangle screenRectangle = window.RectangleToScreen(window.ClientRectangle);
            int titleHeight = screenRectangle.Top - window.Top;
            window.Height += titleHeight + 10;

            customDelimiterTextBox.Width = window.Width - 14;

            window.ShowDialog();

            return Task.CompletedTask;
        }
    }
}
