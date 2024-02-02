// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions.CopyPath
{
    internal sealed class CopyPath : IAction
    {
        private bool _isVisible;

        public string[] SelectedItems
        {
            get => [];
            set
            {
                if (value.Length > 1)
                {
                    _isVisible = true;
                }
            }
        }

        public string Header => "Copy path of files seperated by...";

        public bool HasSubMenu => true;

        public IAction[]? SubMenuItems =>
        [
            new CopyPathSeperatedBySemicolon(),
            new CopyPathSeperatedBySpace(),
            new CopyPathSeperatedByComma(),
            new CopyPathSeperatedByNewline(),
            new CopyPathSeperatedByCustom()
        ];

        public int Category => 2;

        public IconElement? Icon => null;

        public bool IsVisible => _isVisible;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            throw new InvalidOperationException();
        }

        public static void SeperateFilePathByDelimiterAndAddToClipboard(string delimiter, string[] items)
        {
            StringBuilder text = new();

            foreach (string filename in items)
            {
                text.Append(filename);
                text.Append(delimiter);
            }

            text.Length -= delimiter.Length;

            Clipboard.SetText(text.ToString());
        }
    }
}
