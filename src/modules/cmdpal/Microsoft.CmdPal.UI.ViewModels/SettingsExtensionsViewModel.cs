// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Provides filtering over the list of provider settings view models.
/// Intended to be used by the UI to bind a TextBox (SearchText) and an ItemsRepeater (FilteredProviders).
/// </summary>
public partial class SettingsExtensionsViewModel : ObservableObject
{
    private static readonly CompositeFormat LabelNumberExtensionFound
        = CompositeFormat.Parse(Properties.Resources.builtin_settings_extension_n_extensions_found!);

    private static readonly CompositeFormat LabelNumberExtensionInstalled
        = CompositeFormat.Parse(Properties.Resources.builtin_settings_extension_n_extensions_installed!);

    private readonly ObservableCollection<ProviderSettingsViewModel> _source;
    private readonly TaskScheduler _uiScheduler;

    public ObservableCollection<ProviderSettingsViewModel> FilteredProviders { get; } = [];

    private string _searchText = string.Empty;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    public string ItemCounterText
    {
        get
        {
            var hasQuery = !string.IsNullOrWhiteSpace(_searchText);
            var count = hasQuery ? FilteredProviders.Count : _source.Count;
            var format = hasQuery ? LabelNumberExtensionFound : LabelNumberExtensionInstalled;
            return string.Format(CultureInfo.CurrentCulture, format, count);
        }
    }

    public bool ShowManualReloadOverlay
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowNoResultsPanel => !string.IsNullOrWhiteSpace(_searchText) && FilteredProviders.Count == 0;

    public bool HasResults => !ShowNoResultsPanel;

    public IRelayCommand ReloadExtensionsCommand { get; }

    public SettingsExtensionsViewModel(ObservableCollection<ProviderSettingsViewModel> source, TaskScheduler uiScheduler)
    {
        _source = source;
        _uiScheduler = uiScheduler;
        _source.CollectionChanged += Source_CollectionChanged;
        ApplyFilter();

        ReloadExtensionsCommand = new RelayCommand(ReloadExtensions);

        WeakReferenceMessenger.Default.Register<ReloadFinishedMessage>(this, (_, _) =>
        {
            Task.Factory.StartNew(() => ShowManualReloadOverlay = false, CancellationToken.None, TaskCreationOptions.None, _uiScheduler);
        });
    }

    private void Source_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = _searchText;
        var filtered = ListHelpers.FilterList(_source, query, Matches);
        ListHelpers.InPlaceUpdateList(FilteredProviders, filtered);
        OnPropertyChanged(nameof(ItemCounterText));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowNoResultsPanel));
    }

    private static int Matches(string query, ProviderSettingsViewModel item)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 100;
        }

        return Contains(item.DisplayName, query)
               || Contains(item.ExtensionName, query)
               || Contains(item.ExtensionSubtext, query)
            ? 100
            : 0;
    }

    private static bool Contains(string? haystack, string needle)
    {
        return !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void OpenStoreWithExtension(string? query)
    {
        const string extensionsAssocUri = "ms-windows-store://assoc/?Tags=AppExtension-com.microsoft.commandpalette";
        ShellHelpers.OpenInShell(extensionsAssocUri);
    }

    private void ReloadExtensions()
    {
        ShowManualReloadOverlay = true;
        WeakReferenceMessenger.Default.Send<ClearSearchMessage>();
        WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>();
    }
}
