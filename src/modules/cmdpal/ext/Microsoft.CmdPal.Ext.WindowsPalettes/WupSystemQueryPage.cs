// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using Microsoft.CmdPal.Ext.WindowsPalettes.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

// Handles SystemInvokerKind.Query: runs the serialized PaletteQuery from the invoker context against
// every catalog source matching the requested SourceKinds and shows the merged results.
internal sealed partial class WupSystemQueryPage : ListPage, IDisposable
{
    private readonly PaletteItemView _item;
    private readonly string[] _sourceKinds;
    private readonly string? _queryJson;
    private readonly object _stateLock = new();
    private List<WupSourcePage> _sources = [];
    private int _generation;
    private bool _started;

    private WupSystemQueryPage(PaletteItemView item, string[] sourceKinds, string? queryJson, string? commandId)
    {
        _item = item;
        _sourceKinds = sourceKinds;
        _queryJson = queryJson;
        Id = commandId ?? WupCommandId.Create(item.ExtensionId, item.Id, WupCommandKind.Children);
        Name = item.Title ?? string.Empty;
        Title = Name;
        Icon = WupIconAdapter.Create(item.Icon);
        ShowDetails = true;
    }

    internal static WupSystemQueryPage Create(
        PaletteItemView item,
        PaletteSystemInvoker invoker,
        string? commandId)
    {
        string[] sourceKinds = [];
        string? queryJson = null;
        try
        {
            var context = invoker.Context;
            if (context is not null)
            {
                if (context.TryGetValue(KnownPaletteProperties.SourceKinds, out var kinds))
                {
                    sourceKinds = ToStrings(kinds);
                }

                if (context.TryGetValue(KnownPaletteProperties.Query, out var query))
                {
                    queryJson = query as string;
                }
            }
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Failed to read Query invoker context for '{item.ExtensionId}:{item.Id}': 0x{ex.HResult:X8} {ex.Message}");
        }

        return new(item, sourceKinds.Length > 0 ? sourceKinds : WindowsPalettesCommandsProvider.SourceKindSpecs, queryJson, commandId);
    }

    public override IListItem[] GetItems()
    {
        WupSourcePage[] sources;
        int? generationToStart = null;
        lock (_stateLock)
        {
            if (!_started)
            {
                _started = true;
                generationToStart = _generation;
            }

            sources = [.. _sources];
        }

        if (generationToStart is int generation)
        {
            IsLoading = true;
            _ = Task.Run(() => StartAsync(generation));
        }

        return WupResultMerger.Merge(sources);
    }

    // The parent list disposes replaced adapters (and their commands) while this page may still be open,
    // so Dispose resets the page instead of latching it; the next GetItems re-runs the query.
    public void Dispose()
    {
        WupSourcePage[] sources;
        lock (_stateLock)
        {
            _generation++;
            var wasStarted = _started;
            _started = false;
            sources = [.. _sources];
            _sources = [];
            if (!wasStarted && sources.Length == 0)
            {
                return;
            }
        }

        foreach (var source in sources)
        {
            source.ItemsChanged -= Source_ItemsChanged;
            source.Dispose();
        }

        RaiseItemsChanged(0);
    }

    private async Task StartAsync(int generation)
    {
        try
        {
            if (string.IsNullOrEmpty(_queryJson))
            {
                throw new InvalidOperationException(Resources.QueryMissing);
            }

            var catalog = PaletteCatalog.Open(new PaletteKinds(_sourceKinds), string.Empty);
            List<WupSourcePage> sources = [];
            foreach (var definition in catalog.GetPalettes())
            {
                var source = new WupSourcePage(definition);
                source.ItemsChanged += Source_ItemsChanged;
                sources.Add(source);
            }

            bool current;
            lock (_stateLock)
            {
                current = generation == _generation;
                if (current)
                {
                    _sources = sources;
                }
            }

            if (!current)
            {
                foreach (var source in sources)
                {
                    source.ItemsChanged -= Source_ItemsChanged;
                    source.Dispose();
                }

                return;
            }

            WupDiagnostics.Write($"Query invoker for '{_item.ExtensionId}:{_item.Id}' dispatching {sources.Count} source(s).");
            await Task.WhenAll(sources.Select(source => source.ExecuteFixedQueryAsync(PaletteQuery.FromJson(_queryJson)))).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Query invoker for '{_item.ExtensionId}:{_item.Id}' failed: 0x{ex.HResult:X8} {ex.Message}");
            EmptyContent = new CommandItem(new NoOpCommand())
            {
                Title = Resources.QueryFailed,
                Subtitle = ex.Message,
            };
        }
        finally
        {
            IsLoading = false;
            RaiseItemsChanged();
        }
    }

    private void Source_ItemsChanged(object sender, IItemsChangedEventArgs args) => RaiseItemsChanged();

    private static string[] ToStrings(object? value) => value switch
    {
        string text => [text],
        IEnumerable<string> strings => [.. strings.Where(static s => !string.IsNullOrEmpty(s))],
        IEnumerable values => [.. values.OfType<string>().Where(static s => !string.IsNullOrEmpty(s))],
        _ => [],
    };
}
