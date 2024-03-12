// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;
using FileActionsMenu.Helpers.Telemetry;
using Microsoft.PowerToys.Telemetry;
using TaskDialog = Microsoft.WindowsAPICodePack.Dialogs.TaskDialog;
using TaskDialogButton = Microsoft.WindowsAPICodePack.Dialogs.TaskDialogButton;
using TaskDialogProgressBar = Microsoft.WindowsAPICodePack.Dialogs.TaskDialogProgressBar;
using TaskDialogProgressBarState = Microsoft.WindowsAPICodePack.Dialogs.TaskDialogProgressBarState;

namespace FileActionsMenu.Helpers
{
    /// <summary>
    /// A helper class to show progress of file actions.
    /// </summary>
    public class FileActionProgressHelper : IDisposable
    {
        private readonly TaskDialogProgressBar _progressBar;
        private readonly TaskDialog _taskDialog;
        private readonly string _actionName;
        private TaskDialog? _conflictTaskDialog;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileActionProgressHelper"/> class.
        /// Opens a new progress dialog.
        /// </summary>
        /// <param name="actionName">Name of the action.</param>
        /// <param name="count">Number of items.</param>
        /// <param name="onCancel">Action to execute when the action is cancelled.</param>
        public FileActionProgressHelper(string actionName, int count, Action onCancel)
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
                Text = ResourceHelper.GetResource("Progress.Cancel"),
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
                onCancel();
                _taskDialog.Close();
                _conflictTaskDialog?.Close();
            };

            _taskDialog.Closing += (sender, e) =>
            {
                onCancel();
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

        /// <summary>
        /// Shows a conflict dialog.
        /// </summary>
        /// <param name="fileName">The conflicting file.</param>
        /// <param name="onReplace">Action to execute when the user presses "Replace".</param>
        /// <param name="onIgnore">Action to execute when the user presses "Ignore".</param>
        [STAThread]
        public async Task Conflict(string fileName, Action onReplace, Action onIgnore)
        {
            SystemSounds.Exclamation.Play();
            TaskCompletionSource taskCompletionSource = new();

            _conflictTaskDialog?.Close();

#pragma warning disable CA1863 // Use 'CompositeFormat'
            _conflictTaskDialog = new()
            {
                Text = string.Format(CultureInfo.InvariantCulture, ResourceHelper.GetResource("Progress.Conflict.Content"), fileName),
                Caption = ResourceHelper.GetResource("Progress.Conflict.Title"),
            };
#pragma warning restore CA1863 // Use 'CompositeFormat'
            TaskDialogButton replaceButton = new()
            {
                Text = ResourceHelper.GetResource("Progress.Conflict.Replace"),
            };
            replaceButton.Click += (sender, e) =>
            {
                onReplace();
                _progressBar.State = TaskDialogProgressBarState.Normal;
                taskCompletionSource.SetResult();
                _conflictTaskDialog.Close();
                PowerToysTelemetry.Log.WriteEvent(new FileActionsMenuProgressConflictEvent() { ReplaceChosen = true });
            };
            TaskDialogButton ignoreButton = new()
            {
                Text = ResourceHelper.GetResource("Progress.Conflict.Ignore"),
            };
            ignoreButton.Click += (sender, e) =>
            {
                onIgnore();
                _progressBar.State = TaskDialogProgressBarState.Normal;
                taskCompletionSource.SetResult();
                _conflictTaskDialog.Close();
                PowerToysTelemetry.Log.WriteEvent(new FileActionsMenuProgressConflictEvent() { ReplaceChosen = false });
            };
            _conflictTaskDialog.Closing += (sender, e) =>
            {
                if (!taskCompletionSource.Task.IsCompleted)
                {
                    onIgnore();
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
