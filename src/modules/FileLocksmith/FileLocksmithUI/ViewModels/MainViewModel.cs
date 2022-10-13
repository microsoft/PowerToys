// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.FileLocksmithUI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using CommunityToolkit.WinUI;
    using FileLocksmith.Interop;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml.Controls;

#pragma warning disable CA1708 // Identifiers should differ by more than case
    public partial class MainViewModel : ObservableObject, IDisposable
#pragma warning restore CA1708 // Identifiers should differ by more than case
    {
        private ObservableCollection<ProcessResult> _processes;
        private bool _tasksToBeKilled;
        private bool _isLoading;
        private string[] paths;
        private bool _disposed;

        public ObservableCollection<ProcessResult> Processes
        {
            get
            {
                return _processes;
            }

            set
            {
                _processes = value;
                OnPropertyChanged(nameof(Processes));
            }
        }

        public bool TasksToBeKilled
        {
            get
            {
                return _tasksToBeKilled;
            }

            set
            {
                _tasksToBeKilled = value;
                OnPropertyChanged(nameof(TasksToBeKilled));
            }
        }

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

        public MainViewModel()
        {
            LoadProcesses();
        }

        private void LoadProcesses()
        {
            IsLoading = true;

            // paths = NativeMethods.ReadPathsFromStdin();
            paths = new string[1] { "C:\\Program Files" };
            Processes = new ObservableCollection<ProcessResult>(NativeMethods.FindProcessesRecursive(paths));
            IsLoading = false;
        }

        [RelayCommand]
        public void Reload()
        {
            LoadProcesses();
        }

        [RelayCommand]
        public void SelectionChanged(IList<object> selectedItems)
        {
            if (selectedItems.Count >= 0)
            {
                TasksToBeKilled = true;
            }
            else
            {
                TasksToBeKilled = false;
            }

            System.Diagnostics.Debug.WriteLine(TasksToBeKilled);
        }

        [RelayCommand]
        public void KillProcesses(IList<object> selectedItems)
        {
            List<ProcessResult> processesToKill = new ();

            foreach (ProcessResult selectedItem in selectedItems)
            {
                processesToKill.Add(selectedItem);
            }

            foreach (ProcessResult process in processesToKill)
            {
                // if (!NativeMethods.KillProcess(process.pid))
                // {
                    // TODO show something on failure.
                // }
                Processes.Remove(process);
            }

            TasksToBeKilled = false;
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
