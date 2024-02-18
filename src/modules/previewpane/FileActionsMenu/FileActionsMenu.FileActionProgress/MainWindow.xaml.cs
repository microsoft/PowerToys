// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace FileActionsMenu.FileActionProgress
{
    public sealed partial class MainWindow : WindowEx
    {
        private int _total;
        private int _current = -1;

        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            WindowTitle.Text = "File Action Progress";
            FileIndicator.Text = "File Name: C:\\sadasd\\ewdfewfuiohwujfchrewuifhrewuifhcreiufhreiuhfruiehfciu";
            NumberIndicator.Text = "Object 10/300";
            AppTitleTextBlock.Text = "File Action Progress";

            UpdateProgress();
            _ = HandleInput();
        }

#pragma warning disable CA1303 // Do not pass literals as localized parameters
        [DoesNotReturn]
        public async Task HandleInput()
        {
            Console.WriteLine("Ready");

            string line;
            while ((line = await Console.In.ReadLineAsync()) != null)
            {
                string[] parts = line.Split(':', 2);
                string command = parts[0];
                string argument = parts[1].TrimStart();
                DispatcherQueue.TryEnqueue(() => HandleCommand(command, argument));
            }
        }

        private void HandleCommand(string command, string argument)
        {
            switch (command)
            {
                case "Total":
                    ActionProgressBar.Maximum = int.Parse(argument, CultureInfo.InvariantCulture);
                    _total = int.Parse(argument, CultureInfo.InvariantCulture);
                    UpdateProgress();
                    break;
                case "Title":
                    WindowTitle.Text = argument;
                    break;
                case "File":
                    ActionProgressBar.Value++;
                    _current++;
                    FileIndicator.Text = "Current object: " + argument;
                    UpdateProgress();
                    break;
                case "Conflict":
                    CancelButton.IsEnabled = false;
                    Action replaceAction = () =>
                    {
                        CancelButton.IsEnabled = true;
                        Console.WriteLine("Replace");
                    };
                    Action ignoreAction = () =>
                    {
                        CancelButton.IsEnabled = true;
                        Console.WriteLine("Ignore");
                    };
                    var conflictWindow = new FileConflictWindow(argument, replaceAction, ignoreAction);
                    conflictWindow.Show();
                    break;
                case "Close":
                    Close();
                    break;
            }
        }
#pragma warning restore CA1303 // Do not pass literals as localized parameters

        private void UpdateProgress()
        {
            ActionProgressBar.Value = _current;
            NumberIndicator.Text = "Object " + _current + "/" + _total;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
