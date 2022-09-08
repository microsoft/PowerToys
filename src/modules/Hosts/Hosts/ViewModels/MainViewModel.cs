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

namespace Hosts.ViewModels
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

        [ObservableProperty]
        private string _addressFilter;

        [ObservableProperty]
        private string _hostsFilter;

        [ObservableProperty]
        private string _commentFilter;

        [ObservableProperty]
        private bool _filtered;

        [ObservableProperty]
        private string _header;

        private ObservableCollection<Entry> _entries;

        public ObservableCollection<Entry> Entries
        {
            get
            {
                if (_filtered)
                {
                    var filter = _entries.AsEnumerable();

                    if (!string.IsNullOrWhiteSpace(_addressFilter))
                    {
                        filter = filter.Where(e => e.Address.Contains(_addressFilter, StringComparison.OrdinalIgnoreCase));
                    }

                    if (!string.IsNullOrWhiteSpace(_hostsFilter))
                    {
                        filter = filter.Where(e => e.Hosts.Contains(_hostsFilter, StringComparison.OrdinalIgnoreCase));
                    }

                    if (!string.IsNullOrWhiteSpace(_commentFilter))
                    {
                        filter = filter.Where(e => e.Comment.Contains(_commentFilter, StringComparison.OrdinalIgnoreCase));
                    }

                    return new ObservableCollection<Entry>(filter);
                }
                else
                {
                    return _entries;
                }
            }

            set
            {
                _entries = value;
                OnPropertyChanged(nameof(Entries));
            }
        }

        public ICommand ReadHostsCommand => new RelayCommand(ReadHosts);

        public ICommand ApplyFiltersCommand => new RelayCommand(ApplyFilters);

        public ICommand ClearFiltersCommand => new RelayCommand(ClearFilters);

        public MainViewModel()
        {
            _fileSystem = new FileSystem();
            _hostsService = new HostsService(_fileSystem);
            _hostsService.FileChanged += (s, e) =>
            {
                _dispatcherQueue.TryEnqueue(() => FileChanged = true);
            };
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

        public void UpdateHeader(string header)
        {
            _header = header;

            Task.Run(async () =>
            {
                var error = !await _hostsService.WriteAsync(_header, _entries);
                await _dispatcherQueue.EnqueueAsync(() => Error = error);
            });
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
                (_header, var entries) = await _hostsService.ReadAsync();

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

        public void ApplyFilters()
        {
            if (_entries != null)
            {
                Filtered = !string.IsNullOrWhiteSpace(_addressFilter) || !string.IsNullOrWhiteSpace(_hostsFilter) || !string.IsNullOrWhiteSpace(_commentFilter);
                OnPropertyChanged(nameof(Entries));
            }
        }

        public void ClearFilters()
        {
            AddressFilter = null;
            HostsFilter = null;
            CommentFilter = null;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Entry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Task.Run(async () =>
            {
                var error = !await _hostsService.WriteAsync(_header, _entries);
                await _dispatcherQueue.EnqueueAsync(() => Error = error);
            });
        }

        private void Entries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Task.Run(async () =>
            {
                var error = !await _hostsService.WriteAsync(_header, _entries);
                await _dispatcherQueue.EnqueueAsync(() => Error = error);
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
    }
}
