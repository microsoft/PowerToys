// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Media;
using System.Text.RegularExpressions;
using System.Threading;
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
                _conflictTaskDialog = null;
            };

            _taskDialog.Closing += (sender, e) =>
            {
                onCancel();
                _conflictTaskDialog?.Close();
                _conflictTaskDialog = null;
            };
            _taskDialog.StandardButtons = Microsoft.WindowsAPICodePack.Dialogs.TaskDialogStandardButtons.None;
            Task.Run(() =>
            {
                _firstDialogOpened.SetResult();
                _taskDialog.Show();
            });
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
            TaskCompletionSource taskCompletionSource = new();

            _conflictTaskDialog?.Close();

            // Add newline after 45 characters to prevent cutoffs (Thanks copilot)
            string AddNewlines(string input, int maxLength)
            {
                string[] parts = input.Split('\\');
                string result = string.Empty;
                string currentLine = string.Empty;

                foreach (string part in parts)
                {
                    if (currentLine.Length + part.Length + 1 > maxLength)
                    {
                        if (currentLine.Length > 0)
                        {
                            result += currentLine + "\\" + Environment.NewLine;
                        }

                        string tempPart = part;

                        while (tempPart.Length > maxLength)
                        {
                            result += tempPart.AsSpan(0, maxLength).ToString() + Environment.NewLine;
                            tempPart = tempPart.Substring(maxLength);
                        }

                        currentLine = tempPart;
                    }
                    else
                    {
                        if (currentLine.Length > 0)
                        {
                            currentLine += "\\";
                        }

                        currentLine += part;
                    }
                }

                if (currentLine.Length > 0)
                {
                    result += currentLine;
                }

                return result;
            }

            fileName = AddNewlines(fileName, 45);

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
            TaskDialogButton cancelButton = new()
            {
                Text = ResourceHelper.GetResource("Progress.Cancel"),
            };
            cancelButton.Click += (sender, e) =>
            {
                taskCompletionSource.SetResult();
                _taskDialog.Close(Microsoft.WindowsAPICodePack.Dialogs.TaskDialogResult.Cancel);
                _conflictTaskDialog?.Close();
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
            _conflictTaskDialog.Controls.Add(cancelButton);
            Thread t = new(new ThreadStart(async () =>
            {
                await _firstDialogOpened.Task;
                SystemSounds.Exclamation.Play();
                Thread.Sleep(100);
                _conflictTaskDialog.Show();
            }));

            t.Start();
            await taskCompletionSource.Task;
        }

        private TaskCompletionSource _firstDialogOpened = new();

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _taskDialog.Dispose();
        }
    }
}
