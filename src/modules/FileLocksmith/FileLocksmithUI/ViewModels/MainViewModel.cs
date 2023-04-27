// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileLocksmith.Interop;
using ManagedCommon;

namespace PowerToys.FileLocksmithUI.ViewModels
{
#pragma warning disable CA1708 // Identifiers should differ by more than case
    public partial class MainViewModel : ObservableObject, IDisposable
#pragma warning restore CA1708 // Identifiers should differ by more than case
    {
        public IAsyncRelayCommand LoadProcessesCommand { get; }

        private bool _isLoading;
        private bool _isElevated;
        private string[] paths;
        private bool _disposed;
        private CancellationTokenSource _cancelProcessWatching;

        public ObservableCollection<ProcessResult> Processes { get; } = new();

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
            Processes.Clear();

            if (_cancelProcessWatching is not null)
            {
                _cancelProcessWatching.Cancel();
            }

            _cancelProcessWatching = new CancellationTokenSource();

            foreach (ProcessResult p in await FindProcesses(paths))
            {
                Processes.Add(p);
                WatchProcess(p, _cancelProcessWatching.Token);
            }

            IsLoading = false;
        }

        private async Task<List<ProcessResult>> FindProcesses(string[] paths)
        {
            var results = new List<ProcessResult>();
            await Task.Run(() =>
            {
                results = NativeMethods.FindProcessesRecursive(paths).ToList();
            });
            return results;
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
                Processes.Remove(process); // If we couldn't get an handle to the process or it has exited in the meanwhile, don't show it.
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
                Logger.LogError($"Couldn't get an handle to kill process {selectedProcess.name} with PID {selectedProcess.pid}. Likely has been killed already.", ex);
                Processes.Remove(selectedProcess); // If we couldn't get an handle to the process, remove it from the list, since it's likely been killed already.
            }
        }

        [RelayCommand]
        public void RestartElevated()
        {
            if (NativeMethods.StartAsElevated(paths))
            {
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
                    _disposed = true;
                }
            }
        }
    }
}
