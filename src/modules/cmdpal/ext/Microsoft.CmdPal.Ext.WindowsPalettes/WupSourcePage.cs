// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;
using Windows.UI.Palettes;
using WinRT;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal sealed partial class WupSourcePage : DynamicListPage, IDisposable
{
    private static readonly TimeSpan RehydrationTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DismissalTimeout = TimeSpan.FromSeconds(5);

    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _activationGate = new(1, 1);
    private readonly List<WupListItemAdapter> _adapters = [];
    private readonly List<WupListItemAdapter> _rehydratedAdapters = [];
    private readonly TypedEventHandler<IPaletteItemSourceExtension, object> _itemsChangedHandler;
    private IPaletteExtensionDefinition _definition;
    private string _sourceDisplayName;
    private CancellationTokenSource? _queryCancellation;
    private IPaletteItemSourceExtension? _extension;
    private WupExtensionHost? _host;
    private WupAnalyticsSession? _analytics;
    private string? _lastSearchText;
    private PaletteQuery? _lastFixedQuery;
    private long _queryGeneration;
    private bool _disposed;

    internal WupSourcePage(PaletteExtensionDefinition definition)
        : this(definition.As<IPaletteExtensionDefinition>())
    {
    }

    internal WupSourcePage(IPaletteExtensionDefinition definition)
        : this(definition, WupSourceDescriptor.Create(definition))
    {
    }

    internal WupSourcePage(IPaletteExtensionDefinition definition, WupSourceDescriptor descriptor)
    {
        _definition = definition;
        _sourceDisplayName = descriptor.DisplayName;
        Id = descriptor.Id;
        Name = descriptor.DisplayName;
        Title = descriptor.DisplayName;
        PlaceholderText = descriptor.DisplayName;
        Icon = descriptor.Icon;
        ShowDetails = true;
        _itemsChangedHandler = Extension_ItemsChanged;
        WupSession.Ended += Session_Ended;
    }

    internal IPaletteExtensionDefinition Definition => Volatile.Read(ref _definition);

    internal void UpdateDefinition(IPaletteExtensionDefinition definition, WupSourceDescriptor descriptor)
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            Volatile.Write(ref _definition, definition);
            _sourceDisplayName = descriptor.DisplayName;
        }

        Name = descriptor.DisplayName;
        Title = descriptor.DisplayName;
        PlaceholderText = descriptor.DisplayName;
        Icon = descriptor.Icon;
    }

    public override IListItem[] GetItems()
    {
        lock (_stateLock)
        {
            return [.. _adapters];
        }
    }

    internal WupListItemAdapter[] GetAdapters()
    {
        lock (_stateLock)
        {
            return [.. _adapters];
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => _ = ExecuteQueryAsync(newSearch, null);

    internal Task ExecuteSearchAsync(string searchText) => ExecuteQueryAsync(searchText, null);

    internal Task ExecuteFixedQueryAsync(PaletteQuery query) => ExecuteQueryAsync(null, query);

    public override void LoadMore()
    {
    }

    public void Dispose()
    {
        WupListItemAdapter[] rehydratedAdapters;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            rehydratedAdapters = [.. _rehydratedAdapters];
            _rehydratedAdapters.Clear();
        }

        WupSession.Ended -= Session_Ended;
        CancelQuery(null);
        ReplaceItems(null, null);
        ReleaseExtension(endSession: true, itemSelected: false, waitForDismissal: true);
        foreach (var adapter in rehydratedAdapters)
        {
            adapter.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    internal ICommandItem? TryRehydrateCommandItem(WupCommandIdentity identity, string commandId)
    {
        if (!string.Equals(identity.ExtensionId, Definition.ExtensionId, StringComparison.Ordinal))
        {
            return null;
        }

        return TryRehydrateCommandItem(identity.ItemId, identity.Kind, commandId);
    }

    internal ICommandItem? TryRehydrateLegacyCommandItem(string commandId)
    {
        var prefix = $"wup:{Definition.ExtensionId}:";
        if (!commandId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var itemId = commandId[prefix.Length..];
        const string childrenSuffix = ":children";
        if (itemId.EndsWith(childrenSuffix, StringComparison.Ordinal))
        {
            var childrenItemId = itemId[..^childrenSuffix.Length];
            var children = TryRehydrateCommandItem(childrenItemId, WupCommandKind.Children, commandId);
            if (children is not null)
            {
                return children;
            }
        }

        return TryRehydrateCommandItem(itemId, WupCommandKind.Invoke, commandId);
    }

    private CancellationToken CancelQuery(CancellationTokenSource? replacement)
    {
        var previous = Interlocked.Exchange(ref _queryCancellation, replacement);
        previous?.Cancel();
        previous?.Dispose();
        return replacement?.Token ?? CancellationToken.None;
    }

    private async Task ExecuteQueryAsync(string? searchText, PaletteQuery? fixedQuery)
    {
        // The generation, last query and cancellation source are swapped together so concurrent searches and
        // ItemsChanged refreshes can't cancel each other and leave the page with no current query.
        long generation;
        CancellationToken cancellationToken;
        CancellationTokenSource? previous;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            generation = Interlocked.Increment(ref _queryGeneration);
            _lastSearchText = searchText;
            _lastFixedQuery = fixedQuery;
            var cancellation = new CancellationTokenSource();
            cancellationToken = cancellation.Token;
            previous = Interlocked.Exchange(ref _queryCancellation, cancellation);
        }

        // Everything after the swap may call into the source or Command Palette; return to the caller first so a
        // slow source can't hold up dispatch to the other sources.
        await Task.Yield();
        previous?.Cancel();
        previous?.Dispose();
        IsLoading = true;

        try
        {
            var definition = Definition;
            if (fixedQuery is null && string.IsNullOrEmpty(searchText))
            {
                if (!definition.Kinds.IsOfKind("ZeroState.*"))
                {
                    PublishFinal([], null, null, generation, cancellationToken);
                    return;
                }

                var extension = await GetExtensionAsync(cancellationToken).ConfigureAwait(false);
                var items = await Task.Run(extension.GetItems, cancellationToken).ConfigureAwait(false) ?? [];
                WupDiagnostics.Write($"Source '{Id}' returned {items.Length} zero-state items.");
                PublishFinal(items, extension, null, generation, cancellationToken);
                _ = ReportVisibleAsync(extension, items);
                return;
            }

            if (fixedQuery is null && !definition.Kinds.IsOfKind("QueryResult.*"))
            {
                PublishFinal([], null, null, generation, cancellationToken);
                return;
            }

            var source = await GetExtensionAsync(cancellationToken).ConfigureAwait(false);
            var query = fixedQuery ?? CreateQuery(searchText!);
            var operation = source.ExecuteQueryAsync(query);
            // CsWinRT projects empty WinRT arrays as null (microsoft/CsWinRT#2088), so treat null Items as empty.
            operation.Progress = (_, update) => PublishProgress(update?.Items ?? [], source, query, generation, cancellationToken);
            var results = await operation.AsCancellableTask(cancellationToken).ConfigureAwait(false);
            var resultItems = results?.Items ?? [];
            WupDiagnostics.Write($"Source '{Id}' returned {resultItems.Length} query items.");
            PublishFinal(resultItems, source, query, generation, cancellationToken);
            _ = ReportVisibleAsync(source, resultItems);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Source '{Id}' query failed: 0x{ex.HResult:X8} {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            PublishFinal([], null, null, generation, cancellationToken);
        }
    }

    private async Task<IPaletteItemSourceExtension> GetExtensionAsync(CancellationToken cancellationToken)
    {
        var extension = Volatile.Read(ref _extension);
        if (extension is not null)
        {
            return extension;
        }

        await _activationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_extension is not null)
            {
                return _extension;
            }

            var host = new WupExtensionHost(RequestUnload);
            var hostPointer = MarshalInspectable<object>.FromManaged(host);
            try
            {
                var projectedHost = MarshalInspectable<PaletteExtensionHost>.FromAbi(hostPointer);
                var definition = Definition;
                extension = await Task.Run(() => definition.CreateExtension(projectedHost), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Marshal.Release(hostPointer);
            }

            try
            {
                extension.ItemsChanged += _itemsChangedHandler;
            }
            catch (Exception ex)
            {
                WupDiagnostics.Write($"Source '{Id}' ItemsChanged subscription failed: 0x{ex.HResult:X8} {ex.Message}");
            }

            var published = false;
            lock (_stateLock)
            {
                if (!_disposed)
                {
                    _host = host;
                    _analytics = new(extension);
                    Volatile.Write(ref _extension, extension);
                    published = true;
                }
            }

            if (!published)
            {
                // Dispose ran while the source was activating; don't keep it alive or subscribed.
                try
                {
                    extension.ItemsChanged -= _itemsChangedHandler;
                }
                catch (Exception ex)
                {
                    WupDiagnostics.Write($"Source '{Id}' ItemsChanged unsubscription failed: 0x{ex.HResult:X8} {ex.Message}");
                }

                EndSession(host, null, itemSelected: false, waitForDismissal: true);
                throw new ObjectDisposedException(GetType().Name);
            }

            WupDiagnostics.Write($"Activated source '{Id}' in the bridge process.");
            return extension;
        }
        finally
        {
            _activationGate.Release();
        }
    }

    private void Extension_ItemsChanged(IPaletteItemSourceExtension sender, object args)
    {
        string? searchText;
        PaletteQuery? fixedQuery;
        lock (_stateLock)
        {
            if (_disposed || !ReferenceEquals(sender, _extension))
            {
                return;
            }

            searchText = _lastSearchText;
            fixedQuery = _lastFixedQuery;
        }

        WupDiagnostics.Write($"Source '{Id}' raised ItemsChanged; refreshing.");
        _ = Task.Run(() => ExecuteQueryAsync(searchText, fixedQuery));
    }

    private void Session_Ended(bool itemSelected)
    {
        WupExtensionHost? host;
        WupAnalyticsSession? analytics;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            host = _host;
            analytics = _analytics;
        }

        EndSession(host, analytics, itemSelected);
    }

    private void EndSession(WupExtensionHost? host, WupAnalyticsSession? analytics, bool itemSelected, bool waitForDismissal = false)
    {
        if (host is not null)
        {
            var dismissal = Task.Run(host.RaiseDismissed);
            if (waitForDismissal)
            {
                // Teardown must deliver Dismissed before the bridge exits so providers can release cached
                // items and delegates. Normal session endings stay asynchronous; hung sources are bounded.
                try
                {
                    dismissal.WaitAsync(DismissalTimeout).GetAwaiter().GetResult();
                }
                catch (TimeoutException)
                {
                    WupDiagnostics.Write($"Source '{Id}' timed out waiting for Dismissed during teardown.");
                }
            }
        }

        if (analytics is not null)
        {
            _ = ReportConversationAsync(analytics, itemSelected);
        }
    }

    private async Task ReportConversationAsync(WupAnalyticsSession analytics, bool itemSelected)
    {
        try
        {
            await analytics.ReportConversationAsync(itemSelected).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Source '{Id}' conversation analytics failed: 0x{ex.HResult:X8} {ex.Message}");
        }
    }

    private ICommandItem? TryRehydrateCommandItem(
        string itemId,
        WupCommandKind expectedKind,
        string commandId)
    {
        try
        {
            // GetCommandItem is synchronous. Activation runs on the thread pool, bounded by a timeout, so a
            // slow or hung source can't block Command Palette indefinitely.
            using var timeout = new CancellationTokenSource(RehydrationTimeout);
            var activation = Task.Run(() => GetExtensionAsync(timeout.Token), timeout.Token);
            if (!activation.Wait(RehydrationTimeout))
            {
                WupDiagnostics.Write($"Source '{Id}' timed out activating to rehydrate item '{itemId}'.");
                return null;
            }

            var source = activation.Result;
            var item = Task.Run(() => source.GetItem(itemId)).WaitAsync(RehydrationTimeout).GetAwaiter().GetResult();
            if (item is null)
            {
                return null;
            }

            var adapter = new WupListItemAdapter(
                item,
                source,
                query: null,
                _sourceDisplayName,
                GetAnalytics(),
                commandId);
            var command = adapter.Command;
            var actualKind = command is IListPage
                ? WupCommandKind.Children
                : WupCommandKind.Invoke;
            if (command is null ||
                actualKind != expectedKind ||
                !string.Equals(command.Id, commandId, StringComparison.Ordinal))
            {
                adapter.Dispose();
                return null;
            }

            lock (_stateLock)
            {
                if (_disposed)
                {
                    adapter.Dispose();
                    return null;
                }

                _rehydratedAdapters.Add(adapter);
            }

            _ = ReportVisibleAsync(source, [item]);
            return adapter;
        }
        catch (Exception ex)
        {
            var inner = (ex as AggregateException)?.InnerException ?? ex;
            WupDiagnostics.Write(
                $"Source '{Id}' could not rehydrate item '{itemId}': 0x{inner.HResult:X8} {inner.Message}");
            return null;
        }
    }

    private WupAnalyticsSession? GetAnalytics()
    {
        lock (_stateLock)
        {
            return _analytics;
        }
    }

    private void PublishProgress(
        IReadOnlyList<PaletteItemView>? items,
        IPaletteItemSourceExtension source,
        PaletteQuery query,
        long generation,
        CancellationToken cancellationToken)
    {
        if (!IsCurrentQuery(generation, cancellationToken))
        {
            return;
        }

        ReplaceItems(items, source, query);
        RaiseItemsChanged(GetItems().Length);
    }

    private void PublishFinal(
        IReadOnlyList<PaletteItemView>? items,
        IPaletteItemSourceExtension? source,
        PaletteQuery? query,
        long generation,
        CancellationToken cancellationToken)
    {
        if (!IsCurrentQuery(generation, cancellationToken))
        {
            return;
        }

        ReplaceItems(items, source, query);
        HasMoreItems = false;
        IsLoading = false;
        RaiseItemsChanged(GetItems().Length);
    }

    private bool IsCurrentQuery(long generation, CancellationToken cancellationToken) =>
        !_disposed &&
        !cancellationToken.IsCancellationRequested &&
        generation == Volatile.Read(ref _queryGeneration);

    private async Task ReportVisibleAsync(
        IPaletteItemSourceExtension extension,
        IReadOnlyList<PaletteItemView>? items)
    {
        try
        {
            var analytics = GetAnalytics();
            if (analytics is not null && ReferenceEquals(extension, Volatile.Read(ref _extension)))
            {
                await analytics.ReportVisibleAsync(items).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Source '{Id}' visibility analytics failed: 0x{ex.HResult:X8} {ex.Message}");
        }
    }

    private void ReplaceItems(
        IReadOnlyList<PaletteItemView>? items,
        IPaletteItemSourceExtension? source,
        PaletteQuery? query = null)
    {
        List<WupListItemAdapter> replacements = [];
        if (items is not null && source is not null)
        {
            var analytics = GetAnalytics();
            foreach (var item in items)
            {
                replacements.Add(new(item, source, query, _sourceDisplayName, analytics));
            }
        }

        WupListItemAdapter[] oldAdapters;
        lock (_stateLock)
        {
            oldAdapters = [.. _adapters];
            _adapters.Clear();
            _adapters.AddRange(replacements);
        }

        foreach (var adapter in oldAdapters)
        {
            adapter.Dispose();
        }
    }

    private static PaletteQuery CreateQuery(string searchText)
    {
        var culture = CultureInfo.CurrentUICulture;
        return new PaletteQuery
        {
            Arguments = [searchText],
            DisplayLanguageTag = culture.Name,
            SystemLanguageTag = CultureInfo.InstalledUICulture.Name,
            ItemCount = 10,
            Mode = PaletteQueryMode.Search,
        };
    }

    // Called by the source through IPaletteExtensionHost.RequestUnload. The source may call this from
    // inside one of its own callbacks, so the teardown runs asynchronously.
    private void RequestUnload()
    {
        WupDiagnostics.Write($"Source '{Id}' requested unload.");
        _ = Task.Run(UnloadAsync);
    }

    private async Task UnloadAsync()
    {
        try
        {
            await _activationGate.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            if (_disposed)
            {
                return;
            }

            Interlocked.Increment(ref _queryGeneration);
            CancelQuery(null);
            ReplaceItems(null, null);
            ReleaseExtension(endSession: true, itemSelected: false);
            IsLoading = false;
            RaiseItemsChanged(0);
        }
        finally
        {
            try
            {
                _activationGate.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private void ReleaseExtension(bool endSession, bool itemSelected, bool waitForDismissal = false)
    {
        IPaletteItemSourceExtension? extension;
        WupExtensionHost? host;
        WupAnalyticsSession? analytics;
        lock (_stateLock)
        {
            extension = _extension;
            host = _host;
            analytics = _analytics;
            _extension = null;
            _host = null;
            _analytics = null;
        }

        if (endSession)
        {
            EndSession(host, analytics, itemSelected, waitForDismissal);
        }

        if (extension is not null)
        {
            try
            {
                extension.ItemsChanged -= _itemsChangedHandler;
            }
            catch (Exception ex)
            {
                WupDiagnostics.Write($"Source '{Id}' ItemsChanged unsubscription failed: 0x{ex.HResult:X8} {ex.Message}");
            }
        }
    }
}
