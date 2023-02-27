// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Common.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.UI;
using Hosts.Helpers;
using Hosts.Models;
using Hosts.Settings;
using Microsoft.UI.Dispatching;

namespace Hosts.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IHostsService _hostsService;
        private readonly IUserSettings _userSettings;
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        private readonly string[] _loopbackAddresses =
        {
            "127.0.0.1",
            "::1",
            "0:0:0:0:0:0:0:1",
        };

        private bool _readingHosts;
        private bool _disposed;
        private CancellationTokenSource _tokenSource;

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
        private string _additionalLines;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _filtered;

        private bool _showOnlyDuplicates;

        public bool ShowOnlyDuplicates
        {
            get => _showOnlyDuplicates;
            set
            {
                SetProperty(ref _showOnlyDuplicates, value);
                ApplyFilters();
            }
        }

        private ObservableCollection<Entry> _entries;

        public AdvancedCollectionView Entries { get; set; }

        public int NextId => _entries.Max(e => e.Id) + 1;

        public MainViewModel(IHostsService hostService, IUserSettings userSettings)
        {
            _hostsService = hostService;
            _userSettings = userSettings;

            _hostsService.FileChanged += (s, e) => _dispatcherQueue.TryEnqueue(() => FileChanged = true);
            _userSettings.LoopbackDuplicatesChanged += (s, e) => ReadHosts();
        }

        public void Add(Entry entry)
        {
            entry.PropertyChanged += Entry_PropertyChanged;
            _entries.Add(entry);

            FindDuplicates(entry.Address, entry.SplittedHosts);
        }

        public void Update(int index, Entry entry)
        {
            var existingEntry = Entries[index] as Entry;
            var oldAddress = existingEntry.Address;
            var oldHosts = existingEntry.SplittedHosts;

            existingEntry.Address = entry.Address;
            existingEntry.Comment = entry.Comment;
            existingEntry.Hosts = entry.Hosts;
            existingEntry.Active = entry.Active;

            FindDuplicates(oldAddress, oldHosts);
            FindDuplicates(entry.Address, entry.SplittedHosts);
        }

        public void DeleteSelected()
        {
            var address = Selected.Address;
            var hosts = Selected.SplittedHosts;
            _entries.Remove(Selected);

            FindDuplicates(address, hosts);
        }

        public void UpdateAdditionalLines(string lines)
        {
            _additionalLines = lines;

            Task.Run(async () =>
            {
                var error = !await _hostsService.WriteAsync(_additionalLines, _entries);
                await _dispatcherQueue.EnqueueAsync(() => Error = error);
            });
        }

        public void Move(int oldIndex, int newIndex)
        {
            if (Filtered)
            {
                return;
            }

            // Swap the IDs
            var entry1 = _entries[oldIndex];
            var entry2 = _entries[newIndex];
            (entry2.Id, entry1.Id) = (entry1.Id, entry2.Id);

            // Move entries in the UI
            _entries.Move(oldIndex, newIndex);
        }

        [RelayCommand]
        public void ReadHosts()
        {
            if (_readingHosts)
            {
                return;
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                FileChanged = false;
                IsLoading = true;
            });

            Task.Run(async () =>
            {
                _readingHosts = true;
                (_additionalLines, var entries) = await _hostsService.ReadAsync();

                await _dispatcherQueue.EnqueueAsync(() =>
                {
                    _entries = new ObservableCollection<Entry>(entries);

                    foreach (var e in _entries)
                    {
                        e.PropertyChanged += Entry_PropertyChanged;
                    }

                    _entries.CollectionChanged += Entries_CollectionChanged;
                    Entries = new AdvancedCollectionView(_entries, true);
                    Entries.SortDescriptions.Add(new SortDescription(nameof(Entry.Id), SortDirection.Ascending));
                    ApplyFilters();
                    OnPropertyChanged(nameof(Entries));
                    IsLoading = false;
                });
                _readingHosts = false;

                _tokenSource?.Cancel();
                _tokenSource = new CancellationTokenSource();
                FindDuplicates(_tokenSource.Token);
            });
        }

        [RelayCommand]
        public void ApplyFilters()
        {
            var expressions = new List<Expression<Func<object, bool>>>(4);

            if (!string.IsNullOrWhiteSpace(_addressFilter))
            {
                expressions.Add(e => ((Entry)e).Address.Contains(_addressFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(_hostsFilter))
            {
                expressions.Add(e => ((Entry)e).Hosts.Contains(_hostsFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(_commentFilter))
            {
                expressions.Add(e => ((Entry)e).Comment.Contains(_commentFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (_showOnlyDuplicates)
            {
                expressions.Add(e => ((Entry)e).Duplicate);
            }

            Expression<Func<object, bool>> filterExpression = null;

            foreach (var e in expressions)
            {
                filterExpression = filterExpression == null ? e : filterExpression.And(e);
            }

            Filtered = filterExpression != null;
            Entries.Filter = Filtered ? filterExpression.Compile().Invoke : null;
            Entries.RefreshFilter();
        }

        [RelayCommand]
        public void ClearFilters()
        {
            AddressFilter = null;
            HostsFilter = null;
            CommentFilter = null;
            ShowOnlyDuplicates = false;
            Entries.Filter = null;
            Entries.RefreshFilter();
        }

        public async Task PingSelectedAsync()
        {
            var selected = _selected;
            selected.Ping = null;
            selected.Pinging = true;
            selected.Ping = await _hostsService.PingAsync(_selected.Address);
            selected.Pinging = false;
        }

        [RelayCommand]
        public void OpenSettings()
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.Hosts);
        }

        [RelayCommand]
        public void OpenHostsFile()
        {
            _hostsService.OpenHostsFile();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Entry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (Filtered && (e.PropertyName == nameof(Entry.Hosts)
                || e.PropertyName == nameof(Entry.Address)
                || e.PropertyName == nameof(Entry.Comment)
                || e.PropertyName == nameof(Entry.Duplicate)))
            {
                Entries.RefreshFilter();
            }

            // Ping and duplicate should't trigger a file save
            if (e.PropertyName == nameof(Entry.Ping)
                || e.PropertyName == nameof(Entry.Pinging)
                || e.PropertyName == nameof(Entry.Duplicate))
            {
                return;
            }

            Task.Run(async () =>
            {
                var error = !await _hostsService.WriteAsync(_additionalLines, _entries);
                await _dispatcherQueue.EnqueueAsync(() => Error = error);
            });
        }

        private void Entries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Task.Run(async () =>
            {
                var error = !await _hostsService.WriteAsync(_additionalLines, _entries);
                await _dispatcherQueue.EnqueueAsync(() => Error = error);
            });
        }

        private void FindDuplicates(CancellationToken cancellationToken)
        {
            foreach (var entry in _entries)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!_userSettings.LoopbackDuplicates && _loopbackAddresses.Contains(entry.Address))
                    {
                        continue;
                    }

                    SetDuplicate(entry);
                }
                catch (OperationCanceledException)
                {
                    Logger.LogInfo("FindDuplicates cancelled");
                    return;
                }
            }
        }

        private void FindDuplicates(string address, IEnumerable<string> hosts)
        {
            var entries = _entries.Where(e =>
                string.Equals(e.Address, address, StringComparison.InvariantCultureIgnoreCase)
                || hosts.Intersect(e.SplittedHosts, StringComparer.InvariantCultureIgnoreCase).Any());

            foreach (var entry in entries)
            {
                SetDuplicate(entry);
            }
        }

        private void SetDuplicate(Entry entry)
        {
            if (!_userSettings.LoopbackDuplicates && _loopbackAddresses.Contains(entry.Address))
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    entry.Duplicate = false;
                });

                return;
            }

            var hosts = entry.SplittedHosts;

            var duplicate = _entries.FirstOrDefault(e => e != entry
                && e.Type == entry.Type
                && (string.Equals(e.Address, entry.Address, StringComparison.InvariantCultureIgnoreCase)
                    || hosts.Intersect(e.SplittedHosts, StringComparer.InvariantCultureIgnoreCase).Any())) != null;

            _dispatcherQueue.TryEnqueue(() =>
            {
                entry.Duplicate = duplicate;
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
