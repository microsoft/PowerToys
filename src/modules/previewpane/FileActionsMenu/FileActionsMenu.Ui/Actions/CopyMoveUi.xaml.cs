// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    public partial class CopyMoveUi : FluentWindow
    {
        public int Progress
        {
            get => (int)ActionProgressBar.Value;
            set
            {
                ActionProgressBar.Value = value;
            }
        }

        public string CurrentFile
        {
            get => FileIndicator.Text;
            set
            {
                FileIndicator.Text = "Current file: " + value;
            }
        }

        private readonly CancellationTokenSource _cancellationTokenSource;

        public CopyMoveUi(string actionName, int maxProgress, CancellationTokenSource cancellationToken)
        {
            _cancellationTokenSource = cancellationToken;

            InitializeComponent();

            ActionProgressBar.Maximum = maxProgress;

            WindowTitle.Text = actionName;
            ExtendsContentIntoTitleBar = true;
            WindowBackdropType = WindowBackdropType.Mica;

            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this, WindowBackdropType.Mica);
        }

        private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            WindowTitle.Text = "Cancelling...";
            _cancellationTokenSource.Cancel();
        }

        private void FluentWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            WindowTitle.Text = "Cancelling...";
            _cancellationTokenSource.Cancel();
        }
    }
}
