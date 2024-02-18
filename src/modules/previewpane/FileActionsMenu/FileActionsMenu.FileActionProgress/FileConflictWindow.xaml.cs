// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace FileActionsMenu.FileActionProgress
{
    public sealed partial class FileConflictWindow : WindowEx
    {
        private readonly Action _replaceAction;
        private readonly Action _ignoreAction;
        private bool _executed;

        public FileConflictWindow(string name, Action replaceAction, Action ignoreAction)
        {
            _replaceAction = replaceAction;
            _ignoreAction = ignoreAction;

            InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            DescriptionTextElement.Text = $"There is already a file named {name} in this location. Do you want to replace it?";
        }

        private void IgnoreButton_Click(object sender, RoutedEventArgs e)
        {
            _executed = true;
            _ignoreAction();
            Close();
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            _executed = true;
            _replaceAction();
            Close();
        }

        private void Window_Closed(object sender, WindowEventArgs e)
        {
            if (!_executed)
            {
                _ignoreAction();
            }
        }
    }
}
