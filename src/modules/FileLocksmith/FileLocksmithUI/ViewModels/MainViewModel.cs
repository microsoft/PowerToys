// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using PowerToys.FileLocksmithLib.Interop;
using PowerToys.FileLocksmithUI.Helpers;
using PowerToys.FileLocksmithUI.Services;

namespace PowerToys.FileLocksmithUI.ViewModels
{
#pragma warning disable CA1708 // Identifiers should differ by more than case
    public partial class MainViewModel : ObservableObject, IDisposable
#pragma warning restore CA1708 // Identifiers should differ by more than case
    {
        public IAsyncRelayCommand LoadProcessesCommand { get; }

        private readonly FileLocksmithQueryService _queryService = new();
        private bool _isLoading;
        private bool _isElevated;
        private string[] paths;
        private bool _disposed;
        private CancellationTokenSource _cancelProcessWatching;
        private CancellationTokenSource _cancelQuery;
        private string _queryErrorMessage;

        public ObservableCollection<ProcessResult> Processes { get; } = new();

        public string QueryErrorMessage
        {
            get => _queryErrorMessage;
            private set
            {
                _queryErrorMessage = value;
                OnPropertyChanged(nameof(QueryErrorMessage));
                OnPropertyChanged(nameof(HasQueryError));
            }
        }

        public bool HasQueryError => !string.IsNullOrEmpty(QueryErrorMessage);

        public bool IsLoading
        {
            get
            {
                return _isLoading;
            }

            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public bool IsElevated
        {
            get
            {
                return _isElevated;
            }

            set
            {
                _isElevated = value;
                OnPropertyChanged(nameof(IsElevated));
            }
        }

        public string[] Paths
        {
            get => paths;
            set
            {
                paths = value;
                OnPropertyChanged(nameof(Paths));
            }
        }

        public string PathsToString
        {
            get
            {
                return string.Join("\n", paths);
            }
        }

        public MainViewModel()
        {
            paths = NativeMethods.ReadPathsFromFile();
            Logger.LogInfo($"Starting FileLocksmith with {paths.Length} files selected.");
            LoadProcessesCommand = new AsyncRelayCommand(LoadProcessesAsync);
        }

        private async Task LoadProcessesAsync()
        {
            IsLoading = true;
            QueryErrorMessage = null;
            Processes.Clear();

            _cancelProcessWatching?.Cancel();
            _cancelProcessWatching?.Dispose();
            _cancelProcessWatching = new CancellationTokenSource();

            _cancelQuery?.Cancel();
            _cancelQuery?.Dispose();
            _cancelQuery = new CancellationTokenSource();
            var cancellationToken = _cancelQuery.Token;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var queryResult = await _queryService.FindProcessesAsync(paths, cancellationToken);
                stopwatch.Stop();

                if (queryResult.Status == FileLocksmithQueryStatus.Success)
                {
                    Logger.LogInfo($"File Locksmith worker query completed in {stopwatch.ElapsedMilliseconds} ms with exit code 0 and {queryResult.Processes.Count} processes.");
                    foreach (var processInfo in queryResult.Processes)
                    {
                        var process = new ProcessResult(processInfo.Name, processInfo.Pid, processInfo.User, processInfo.Files);
                        Processes.Add(process);
                        WatchProcess(process, _cancelProcessWatching.Token);
                    }
                }
                else
                {
                    var exitCode = queryResult.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unavailable";
                    Logger.LogError($"File Locksmith worker query failed at stage enumeration after {stopwatch.ElapsedMilliseconds} ms with status {queryResult.Status} and exit code {exitCode}.");
                    QueryErrorMessage = queryResult.Status == FileLocksmithQueryStatus.TimedOut
                        ? ResourceLoaderInstance.ResourceLoader.GetString("QueryTimeoutError")
                        : ResourceLoaderInstance.ResourceLoader.GetString("QueryFailedError");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInfo($"File Locksmith worker query canceled at stage enumeration after {stopwatch.ElapsedMilliseconds} ms.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void WatchProcess(ProcessResult process, CancellationToken token)
        {
            try
            {
                Process handle = Process.GetProcessById((int)process.pid);
                try
                {
                    await handle.WaitForExitAsync(token);
                }
                catch (TaskCanceledException)
                {
                    // Nothing to do, normal operation
                }

                if (handle.HasExited)
                {
                    Processes.Remove(process);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Couldn't add a waiter to wait for a process to exit. PID = {process.pid} and Name = {process.name}.", ex);
                Processes.Remove(process); // If we couldn't get a handle to the process or it has exited in the meanwhile, don't show it.
            }
        }

        [RelayCommand]
        public void EndTask(ProcessResult selectedProcess)
        {
            try
            {
                Process handle = Process.GetProcessById((int)selectedProcess.pid);
                try
                {
                    handle.Kill();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Couldn't kill process {selectedProcess.name} with PID {selectedProcess.pid}.", ex);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Couldn't get a handle to kill process {selectedProcess.name} with PID {selectedProcess.pid}. Likely has been killed already.", ex);
                Processes.Remove(selectedProcess); // If we couldn't get a handle to the process, remove it from the list, since it's likely been killed already.
            }
        }

        [RelayCommand]
        public async Task RestartElevated()
        {
            if (NativeMethods.StartAsElevated(paths))
            {
                _cancelQuery?.Cancel();
                if (LoadProcessesCommand.ExecutionTask is not null)
                {
                    await LoadProcessesCommand.ExecutionTask;
                }

                // TODO gentler exit
                Environment.Exit(0);
            }
            else
            {
                // TODO report error?
                Logger.LogError($"Couldn't restart as elevated.");
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cancelQuery?.Cancel();
                    _cancelQuery?.Dispose();
                    _cancelProcessWatching?.Cancel();
                    _cancelProcessWatching?.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}
