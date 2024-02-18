// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace FileActionsMenu.Helpers
{
    public class FileActionProgressHelper : IDisposable
    {
        private readonly StreamWriter _standardInput;

        public event EventHandler? OnReady;

        private Process _process;

        private TaskCompletionSource<ConflictAction> _taskCompletionSource;

        public FileActionProgressHelper()
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "WinUi3Apps\\PowerToys.FileActionsMenu.FileActionProgress.exe"),
                WorkingDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "WinUi3Apps"),
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                /*RedirectStandardError = true,*/
            };

            _process = new()
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            _taskCompletionSource = new TaskCompletionSource<ConflictAction>();
            _process.Start();
            _process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null && e.Data == "Ready")
                {
                    OnReady?.Invoke(this, EventArgs.Empty);
                }
                else if (e.Data != null && e.Data.StartsWith("Ignore", StringComparison.InvariantCulture))
                {
                    _taskCompletionSource.SetResult(ConflictAction.Ignore);
                }
                else if (e.Data != null && e.Data.StartsWith("Replace", StringComparison.InvariantCulture))
                {
                    _taskCompletionSource.SetResult(ConflictAction.Replace);
                }
            };

            // _process.BeginOutputReadLine();
            _standardInput = _process.StandardInput;
        }

        private void SendCommand(string command, string argument)
        {
            _standardInput.WriteLine($"{command}:{argument}");
        }

        public void SetTotal(int total)
        {
            SendCommand("Total", total.ToString(CultureInfo.InvariantCulture));
        }

        public void SetTitle(string title)
        {
            SendCommand("Title", title);
        }

        public void SetCurrentObjectName(string file)
        {
            SendCommand("File", file);
        }

        public enum ConflictAction
        {
            Replace,
            Ignore,
        }

        public async Task<ConflictAction> ShowConflictWindow(string file)
        {
            _taskCompletionSource = new TaskCompletionSource<ConflictAction>();
            SendCommand("Conflict", file);
            return await _taskCompletionSource.Task;
        }

        public void Close()
        {
            SendCommand("Close", string.Empty);
        }

        public void Dispose()
        {
            SendCommand("Close", string.Empty);
            GC.SuppressFinalize(this);
            _process.Dispose();
        }
    }
}
