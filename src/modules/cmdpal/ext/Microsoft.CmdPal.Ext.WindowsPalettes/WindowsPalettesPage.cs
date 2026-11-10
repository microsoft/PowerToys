// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowsPalettes.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI.Palettes;
using WinRT;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal sealed partial class WindowsPalettesPage : DynamicListPage, IDisposable
{
    internal const string PageId = "com.microsoft.cmdpal.wup.windows-palettes";
    public static readonly IconInfo PalettesIcon = new("🎨");

    private readonly object _stateLock = new();
    private readonly object _sourceUpdateLock = new();
    private readonly SemaphoreSlim _searchDispatchGate = new(1, 1);
    private List<WupSourcePage> _sources = [];
    private long _searchGeneration;
    private bool _started;
    private bool _disposed;

    internal WindowsPalettesPage(IReadOnlyList<PaletteExtensionDefinition> definitions)
    {
        Id = PageId;
        Name = Resources.PalettesPageName;
        Title = Resources.ProviderDisplayName;
        PlaceholderText = Resources.SearchPlaceholder;
        ShowDetails = true;
        UpdateSources(definitions);
        Icon = PalettesIcon;
    }

    public override IListItem[] GetItems()
    {
        EnsureStarted();
        return WupResultMerger.Merge(GetSourcesSnapshot());
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        ExtensionHost.LogMessage($"WindowsPalettesPage.UpdateSearchText: '{oldSearch}' -> '{newSearch}'");
        WupSourcePage[] sources;
        long generation;
        lock (_stateLock)
        {
            _started = true;
            sources = [.. _sources];
            generation = ++_searchGeneration;
        }

        IsLoading = true;
        _ = Task.Run(() => SearchSourcesAsync(sources, newSearch, generation));
    }

    public override void LoadMore()
    {
    }

    internal ICommandItem? TryRehydrateCommandItem(string commandId)
    {
        var sources = GetSourcesSnapshot();
        if (WupCommandId.TryParse(commandId, out var identity))
        {
            foreach (var source in sources)
            {
                var item = source.TryRehydrateCommandItem(identity, commandId);
                if (item is not null)
                {
                    return item;
                }
            }

            return null;
        }

        foreach (var source in sources)
        {
            var item = source.TryRehydrateLegacyCommandItem(commandId);
            if (item is not null)
            {
                return item;
            }
        }

        return null;
    }

    internal void UpdateSources(IReadOnlyList<PaletteExtensionDefinition> definitions) =>
        UpdateSources(definitions.Select(definition => definition.As<IPaletteExtensionDefinition>()).ToArray());

    internal void UpdateSources(IReadOnlyList<IPaletteExtensionDefinition> definitions)
    {
        // Serialize catalog refreshes without holding _stateLock while metadata changes or teardown raise events.
        lock (_sourceUpdateLock)
        {
            Dictionary<string, WupSourcePage> remainingSources;
            lock (_stateLock)
            {
                if (_disposed)
                {
                    return;
                }

                remainingSources = _sources.ToDictionary(source => source.Id, StringComparer.Ordinal);
            }

            var updates = definitions.Select(definition => (Definition: definition, Descriptor: WupSourceDescriptor.Create(definition))).ToArray();
            List<WupSourcePage> newSources = new(definitions.Count);
            List<WupSourcePage> addedSources = [];
            foreach (var (definition, descriptor) in updates)
            {
                if (remainingSources.Remove(descriptor.Id, out var source))
                {
                    source.UpdateDefinition(definition, descriptor);
                }
                else
                {
                    source = new WupSourcePage(definition, descriptor);
                    source.ItemsChanged += Source_ItemsChanged;
                    source.PropChanged += Source_PropChanged;
                    addedSources.Add(source);
                }

                newSources.Add(source);
            }

            WupSourcePage[] sources;
            string searchText;
            long generation;
            bool started;
            bool disposed;
            lock (_stateLock)
            {
                disposed = _disposed;
                if (disposed)
                {
                    sources = [];
                    searchText = string.Empty;
                    generation = _searchGeneration;
                    started = false;
                }
                else
                {
                    _sources = newSources;
                    sources = [.. newSources];
                    searchText = SearchText;
                    generation = ++_searchGeneration;
                    started = _started;
                }
            }

            DisposeSources(disposed ? addedSources : remainingSources.Values);
            if (disposed)
            {
                return;
            }

            UpdateState();
            RaiseItemsChanged(GetItemCount());
            if (started)
            {
                IsLoading = true;
                _ = Task.Run(() => SearchSourcesAsync(sources, searchText, generation));
            }
        }
    }

    public void Dispose()
    {
        WupSourcePage[] sources;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _searchGeneration++;
            sources = [.. _sources];
            _sources = [];
        }

        DisposeSources(sources);
        GC.SuppressFinalize(this);
    }

    private void EnsureStarted()
    {
        WupSourcePage[] sources;
        long generation;
        lock (_stateLock)
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            sources = [.. _sources];
            generation = ++_searchGeneration;
        }

        IsLoading = true;
        _ = Task.Run(() => SearchSourcesAsync(sources, string.Empty, generation));
    }

    private void Source_ItemsChanged(object sender, IItemsChangedEventArgs args)
    {
        UpdateState();
        RaiseItemsChanged(GetItemCount());
    }

    private void Source_PropChanged(object sender, IPropChangedEventArgs args) => UpdateState();

    private void UpdateState()
    {
        var sources = GetSourcesSnapshot();
        var isLoading = false;
        foreach (var source in sources)
        {
            isLoading |= source.IsLoading;
        }

        IsLoading = isLoading;
        HasMoreItems = false;
    }

    private int GetItemCount() => WupResultMerger.Merge(GetSourcesSnapshot()).Length;

    private async Task SearchSourcesAsync(WupSourcePage[] sources, string searchText, long generation)
    {
        Task[] searches;
        await _searchDispatchGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!IsCurrentSearch(generation))
            {
                return;
            }

            WupDiagnostics.Write($"Search generation {generation} dispatching {sources.Length} source(s).");
            searches = new Task[sources.Length];
            for (var index = 0; index < sources.Length; index++)
            {
                var source = sources[index];
                ExtensionHost.LogMessage($"WindowsPalettesPage: searching source '{source.Id}' ('{searchText}')");
                WupDiagnostics.Write($"Search generation {generation} dispatching source '{source.Id}'.");
                searches[index] = source.ExecuteSearchAsync(searchText);
            }
        }
        finally
        {
            _searchDispatchGate.Release();
        }

        try
        {
            await Task.WhenAll(searches).ConfigureAwait(false);
            WupDiagnostics.Write($"Search generation {generation} completed all source tasks.");
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Search generation {generation} fan-out failed: 0x{ex.HResult:X8} {ex.Message}");
        }

        if (IsCurrentSearch(generation))
        {
            UpdateState();
        }
    }

    private bool IsCurrentSearch(long generation)
    {
        lock (_stateLock)
        {
            return !_disposed && generation == _searchGeneration;
        }
    }

    internal WupSourcePage[] GetSourcesSnapshot()
    {
        lock (_stateLock)
        {
            return _disposed ? [] : [.. _sources];
        }
    }

    private void DisposeSources(IEnumerable<WupSourcePage> sources)
    {
        foreach (var source in sources)
        {
            source.ItemsChanged -= Source_ItemsChanged;
            source.PropChanged -= Source_PropChanged;
            source.Dispose();
        }
    }
}
