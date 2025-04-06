// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Collections;
using HostsUILib.Exceptions;
using HostsUILib.Helpers;
using HostsUILib.Models;
using HostsUILib.Settings;
using Microsoft.UI.Dispatching;

using static HostsUILib.Settings.IUserSettings;

namespace HostsUILib.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IHostsService _hostsService;
        private readonly IUserSettings _userSettings;
        private readonly IDuplicateService _duplicateService;
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        private bool _readingHosts;

        [ObservableProperty]
        private Entry _selected;

        [ObservableProperty]
        private bool _error;

        [ObservableProperty]
        private string _errorMessage;

        [ObservableProperty]
        private bool _isReadOnly;

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

        [ObservableProperty]
        private bool _showOnlyDuplicates;

        [ObservableProperty]
        private bool _showSplittedEntriesTooltip;

        partial void OnShowOnlyDuplicatesChanged(bool value)
        {
            ApplyFilters();
        }

        private ObservableCollection<Entry> _entries;

        public AdvancedCollectionView Entries { get; set; }

        public int NextId => _entries?.Count > 0 ? _entries.Max(e => e.Id) + 1 : 0;

        public IUserSettings UserSettings => _userSettings;

        public static MainViewModel Instance { get; set; }

        private OpenSettingsFunction _openSettingsFunction;

        public MainViewModel(
            IHostsService hostService,
            IUserSettings userSettings,
            IDuplicateService duplicateService,
            ILogger logger,
            OpenSettingsFunction openSettingsFunction)
        {
            _hostsService = hostService;
            _userSettings = userSettings;
            _duplicateService = duplicateService;

            _hostsService.FileChanged += (s, e) => _dispatcherQueue.TryEnqueue(() => FileChanged = true);
            _userSettings.LoopbackDuplicatesChanged += (s, e) => ReadHosts();

            LoggerInstance.Logger = logger;
            _openSettingsFunction = openSettingsFunction;
        }

        public void Add(Entry entry)
        {
            entry.PropertyChanged += Entry_PropertyChanged;
            _entries.Add(entry);
            _duplicateService.CheckDuplicates(entry.Address, entry.SplittedHosts);
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

            _duplicateService.CheckDuplicates(oldAddress, oldHosts);
            _duplicateService.CheckDuplicates(entry.Address, entry.SplittedHosts);
        }

        public void DeleteSelected()
        {
            var address = Selected.Address;
            var hosts = Selected.SplittedHosts;
            _entries.Remove(Selected);
            _duplicateService.CheckDuplicates(address, hosts);
        }

        public void UpdateAdditionalLines(string lines)
        {
            AdditionalLines = lines;
            _ = Task.Run(SaveAsync);
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
        public void DeleteEntry(Entry entry)
        {
            if (entry is not null)
            {
                var address = entry.Address;
                var hosts = entry.SplittedHosts;
                _entries.Remove(entry);
                _duplicateService.CheckDuplicates(address, hosts);
            }
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
                var data = await _hostsService.ReadAsync();

                await _dispatcherQueue.EnqueueAsync(() =>
                {
                    ShowSplittedEntriesTooltip = data.SplittedEntries;
                    AdditionalLines = data.AdditionalLines;
                    _entries = new ObservableCollection<Entry>(data.Entries);

                    foreach (var e in _entries)
                    {
                        e.PropertyChanged += Entry_PropertyChanged;
                    }

                    _entries.CollectionChanged += Entries_CollectionChanged;
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
                    Entries = new AdvancedCollectionView(_entries, true);
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
                    Entries.SortDescriptions.Add(new SortDescription(nameof(Entry.Id), SortDirection.Ascending));
                    ApplyFilters();
                    OnPropertyChanged(nameof(Entries));
                    IsLoading = false;
                });
                _readingHosts = false;

                _duplicateService.Initialize(_entries);
            });
        }

        [RelayCommand]
        public void ApplyFilters()
        {
            var expressions = new List<Expression<Func<object, bool>>>(4);

            if (!string.IsNullOrWhiteSpace(AddressFilter))
            {
                expressions.Add(e => ((Entry)e).Address.Contains(AddressFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(HostsFilter))
            {
                expressions.Add(e => ((Entry)e).Hosts.Contains(HostsFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(CommentFilter))
            {
                expressions.Add(e => ((Entry)e).Comment.Contains(CommentFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (ShowOnlyDuplicates)
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
            ApplyFilters();
        }

        public async Task PingSelectedAsync()
        {
            var selected = Selected;
            selected.Ping = null;
            selected.Pinging = true;
            selected.Ping = await _hostsService.PingAsync(Selected.Address);
            selected.Pinging = false;
        }

        [RelayCommand]
        public void OpenSettings()
        {
            _openSettingsFunction();
        }

        [RelayCommand]
        public void OpenHostsFile()
        {
            _hostsService.OpenHostsFile();
        }

        [RelayCommand]
        public void OverwriteHosts()
        {
            _hostsService.RemoveReadOnlyAttribute();
            _ = Task.Run(SaveAsync);
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

            // Ping and duplicate should not trigger a file save
            if (e.PropertyName == nameof(Entry.Ping)
                || e.PropertyName == nameof(Entry.Pinging)
                || e.PropertyName == nameof(Entry.Duplicate))
            {
                return;
            }

            _ = Task.Run(SaveAsync);
        }

        private void Entries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            _ = Task.Run(SaveAsync);
        }

        private async Task SaveAsync()
        {
            bool error = true;
            string errorMessage = string.Empty;
            bool isReadOnly = false;

            try
            {
                await _hostsService.WriteAsync(AdditionalLines, _entries);
                error = false;
            }
            catch (NotRunningElevatedException)
            {
                var resourceLoader = ResourceLoaderInstance.ResourceLoader;
                errorMessage = resourceLoader.GetString("FileSaveError_NotElevated");
            }
            catch (ReadOnlyHostsException)
            {
                isReadOnly = true;
                var resourceLoader = ResourceLoaderInstance.ResourceLoader;
                errorMessage = resourceLoader.GetString("FileSaveError_ReadOnly");
            }
            catch (IOException ex) when ((ex.HResult & 0x0000FFFF) == 32)
            {
                // There are some edge cases where a big hosts file is being locked by svchost.exe https://github.com/microsoft/PowerToys/issues/28066
                var resourceLoader = ResourceLoaderInstance.ResourceLoader;
                errorMessage = resourceLoader.GetString("FileSaveError_FileInUse");
            }
            catch (Exception ex)
            {
                LoggerInstance.Logger.LogError("Failed to save hosts file", ex);
                var resourceLoader = ResourceLoaderInstance.ResourceLoader;
                errorMessage = resourceLoader.GetString("FileSaveError_Generic");
            }

            await _dispatcherQueue.EnqueueAsync(() =>
            {
                Error = error;
                ErrorMessage = errorMessage;
                IsReadOnly = isReadOnly;
            });
        }
    }
}
