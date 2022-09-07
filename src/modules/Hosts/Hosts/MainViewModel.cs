// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using Hosts.Helpers;
using Hosts.Models;
using Microsoft.UI.Dispatching;

namespace Hosts
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IFileSystem _fileSystem;
        private readonly HostsService _hostsService;
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        private bool _disposed;

        [ObservableProperty]
        private Entry _selected;

        [ObservableProperty]
        private bool _error;

        [ObservableProperty]
        private bool _fileChanged;

        private ObservableCollection<Entry> _entries;

        public ObservableCollection<Entry> Entries
        {
            get => _entries;
            set
            {
                _entries = value;
                OnPropertyChanged(nameof(Entries));
            }
        }

        public ICommand ReadHostsCommand => new RelayCommand(ReadHosts);

        public MainViewModel()
        {
            _fileSystem = new FileSystem();
            _hostsService = new HostsService(_fileSystem);
            _hostsService.FileChanged += (s, e) =>
            {
                _dispatcherQueue.TryEnqueue(() => FileChanged = true);
            };
        }

        private void Entry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Task.Run(async () =>
            {
                var error = !await _hostsService.WriteAsync(Entries);
                await _dispatcherQueue.EnqueueAsync(() => Error = error);
            });
        }

        private void Entries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Task.Run(async () =>
            {
                var error = !await _hostsService.WriteAsync(Entries);
                await _dispatcherQueue.EnqueueAsync(() => Error = error);
            });
        }

        public void Add(Entry entry)
        {
            entry.PropertyChanged += Entry_PropertyChanged;
            _entries.Add(entry);
        }

        public void Update(int index, Entry entry)
        {
            var existingEntry = _entries.ElementAt(index);
            existingEntry.Address = entry.Address;
            existingEntry.Comment = entry.Comment;
            existingEntry.Hosts = entry.Hosts;
            existingEntry.Active = entry.Active;
        }

        public void DeleteSelected()
        {
            _entries.Remove(Selected);
        }

        public void EnableSelected()
        {
            Selected.Active = true;
        }

        public void DisableSelected()
        {
            Selected.Active = false;
        }

        public void ReadHosts()
        {
            FileChanged = false;

            Task.Run(async () =>
            {
                var entries = await _hostsService.ReadAsync();

                await _dispatcherQueue.EnqueueAsync(() =>
                {
                    Entries = new ObservableCollection<Entry>(entries);

                    foreach (var e in _entries)
                    {
                        e.PropertyChanged += Entry_PropertyChanged;
                    }

                    _entries.CollectionChanged += Entries_CollectionChanged;
                });
            });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _hostsService?.Dispose();
                    _disposed = true;
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
