// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskDialog = Microsoft.WindowsAPICodePack.Dialogs.TaskDialog;
using TaskDialogButton = Microsoft.WindowsAPICodePack.Dialogs.TaskDialogButton;
using TaskDialogProgressBar = Microsoft.WindowsAPICodePack.Dialogs.TaskDialogProgressBar;
using TaskDialogProgressBarState = Microsoft.WindowsAPICodePack.Dialogs.TaskDialogProgressBarState;

namespace FileActionsMenu.Helpers
{
    public class FileActionProgressHelper : IDisposable
    {
        private readonly TaskDialogProgressBar _progressBar;
        private readonly TaskDialog _taskDialog;
        private readonly string _actionName;
        private TaskDialog? _conflictTaskDialog;

        public FileActionProgressHelper(string actionName, int count, Action onClose)
        {
            _actionName = actionName;

            Application.EnableVisualStyles();
            _progressBar = new()
            {
                State = TaskDialogProgressBarState.Normal,
                Maximum = count,
                Minimum = 0,
                Value = 0,
            };

            TaskDialogButton cancelButton = new()
            {
                Text = "Cancel",
            };

            _taskDialog = new()
            {
                ProgressBar = _progressBar,
                Caption = actionName,
                Text = actionName,
                Cancelable = true,
                Controls = { cancelButton },
            };

            cancelButton.Click += (sender, e) =>
            {
                onClose();
                _taskDialog.Close();
                _conflictTaskDialog?.Close();
            };

            _taskDialog.Closing += (sender, e) =>
            {
                onClose();
                _conflictTaskDialog?.Close();
            };
            _taskDialog.StandardButtons = Microsoft.WindowsAPICodePack.Dialogs.TaskDialogStandardButtons.None;
            Task.Run(() => _taskDialog.Show());
        }

        public void UpdateProgress(int current, string fileName)
        {
            _progressBar.Value = current;
            _taskDialog.Text = $"{_actionName}: {fileName}";
        }

        [STAThread]
        public async Task Conflict(string fileName, Action onReplace, Action onIgnore)
        {
            SystemSounds.Exclamation.Play();
            TaskCompletionSource taskCompletionSource = new();
            _conflictTaskDialog = new()
            {
                Text = $"Conflict: {fileName} already exists",
                Caption = "Conflict",
            };
            TaskDialogButton replaceButton = new()
            {
                Text = "Replace",
            };
            replaceButton.Click += (sender, e) =>
            {
                onReplace();
                _progressBar.State = TaskDialogProgressBarState.Normal;
                taskCompletionSource.SetResult();
                _conflictTaskDialog.Close();
            };
            TaskDialogButton ignoreButton = new()
            {
                Text = "Ignore",
            };
            ignoreButton.Click += (sender, e) =>
            {
                onIgnore();
                _progressBar.State = TaskDialogProgressBarState.Normal;
                taskCompletionSource.SetResult();
                _conflictTaskDialog.Close();
            };
            _conflictTaskDialog.Closing += (sender, e) =>
            {
                if (!taskCompletionSource.Task.IsCanceled)
                {
                    taskCompletionSource.SetResult();
                }
            };
            _progressBar.State = TaskDialogProgressBarState.Paused;
            _conflictTaskDialog.Controls.Add(replaceButton);
            _conflictTaskDialog.Controls.Add(ignoreButton);
            _ = Task.Run(_conflictTaskDialog.Show);

            await taskCompletionSource.Task;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _taskDialog.Dispose();
        }
    }
}
