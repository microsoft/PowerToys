// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Messages;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.WindowWalker.Pages;

internal sealed partial class WindowWalkerListPage : DynamicListPage, IDisposable, IRecipient<RefreshWindowsMessage>
{
    private static readonly TimeSpan IconLoadDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan MinimumRefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WarmRefreshDelay = TimeSpan.FromMilliseconds(250);

    private readonly Lock _cacheLock = new();
    private readonly Lock _iconLoadLock = new();
    private readonly Lock _refreshLock = new();
    private readonly WindowWalkerListItem _explorerInfoItem = ResultHelper.GetExplorerInfoResult();
    private Dictionary<WindowKey, WindowWalkerListItem> _itemCache = [];
    private WindowEntry[] _windowEntries = [];
    private CancellationTokenSource? _iconLoadCancellationTokenSource;
    private CancellationTokenSource? _refreshCancellationTokenSource;
    private long _lastRefreshTimestamp;
    private bool _disposed;

    public WindowWalkerListPage()
    {
        Icon = Icons.WindowWalkerIcon;
        Name = Resources.windowwalker_name;
        Id = "com.microsoft.cmdpal.windowwalker";
        PlaceholderText = Resources.windowwalker_PlaceholderText;

        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icon,
            Title = Resources.window_walker_top_level_command_title,
            Subtitle = Resources.windowwalker_NoResultsMessage,
        };

        WeakReferenceMessenger.Default.Register<RefreshWindowsMessage>(this);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems()
    {
        var query = SearchText;
        var entries = GetCachedEntries();
        if (entries.Length == 0 || (string.IsNullOrWhiteSpace(query) && IsSnapshotStale()))
        {
            RequestRefresh(force: false);
        }

        var results = Filter(entries, query);
        QueueIconLoads(results);
        return results;
    }

    public void Receive(RefreshWindowsMessage message)
    {
        if (_disposed)
        {
            return;
        }

        if (!message.Delay)
        {
            RequestRefresh(force: true);
        }
        else
        {
            _ = Task.Run(
                async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                    if (!_disposed)
                    {
                        RequestRefresh(force: true);
                    }
                },
                CancellationToken.None);
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private WindowWalkerListItem[] Filter(WindowEntry[] entries, string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query))
        {
            if (!SettingsManager.Instance.InMruOrder)
            {
                entries = (WindowEntry[])entries.Clone();
                Array.Sort(
                    entries,
                    static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Item.Title, right.Item.Title));
            }

            return AddExplorerInfoIfNeeded(entries);
        }

        var scored = ListHelpers.FilterListWithScores(entries, query, ScoreFunction);
        var filteredEntries = new List<WindowEntry>(entries.Length);
        foreach (var result in scored)
        {
            filteredEntries.Add(result.Item);
        }

        return AddExplorerInfoIfNeeded(filteredEntries);
    }

    private WindowEntry[] GetCachedEntries()
    {
        lock (_cacheLock)
        {
            return _windowEntries;
        }
    }

    private bool IsSnapshotStale()
    {
        var lastRefreshTimestamp = Volatile.Read(ref _lastRefreshTimestamp);
        return lastRefreshTimestamp == 0 || Stopwatch.GetElapsedTime(lastRefreshTimestamp) >= MinimumRefreshInterval;
    }

    private void RequestRefresh(bool force)
    {
        if (_disposed)
        {
            return;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        CancellationTokenSource? previousCancellationTokenSource;
        lock (_refreshLock)
        {
            previousCancellationTokenSource = _refreshCancellationTokenSource;
            if (previousCancellationTokenSource is not null && !force)
            {
                cancellationTokenSource.Dispose();
                return;
            }

            _refreshCancellationTokenSource = cancellationTokenSource;
        }

        previousCancellationTokenSource?.Cancel();

        var hasCachedEntries = GetCachedEntries().Length > 0;
        if (!hasCachedEntries)
        {
            IsLoading = true;
        }

        var delay = !force && hasCachedEntries ? WarmRefreshDelay : TimeSpan.Zero;
        _ = Task.Run(() => RefreshAsync(cancellationTokenSource, delay), CancellationToken.None);
    }

    private async Task RefreshAsync(CancellationTokenSource cancellationTokenSource, TimeSpan delay)
    {
        var cancellationToken = cancellationTokenSource.Token;
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            WindowWalkerCommandsProvider.VirtualDesktopHelperInstance.UpdateDesktopList();
            cancellationToken.ThrowIfCancellationRequested();

            OpenWindows.Instance.UpdateOpenWindowsList(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var entries = Reconcile(OpenWindows.Instance.Windows, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            lock (_cacheLock)
            {
                _windowEntries = entries;
            }

            Volatile.Write(ref _lastRefreshTimestamp, Stopwatch.GetTimestamp());
            SetLoadingComplete(cancellationTokenSource);
            RaiseItemsChanged(entries.Length);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage { Message = $"Failed to refresh the Window Walker list: {ex.Message}" });
        }
        finally
        {
            var isCurrentRefresh = false;
            lock (_refreshLock)
            {
                if (ReferenceEquals(_refreshCancellationTokenSource, cancellationTokenSource))
                {
                    _refreshCancellationTokenSource = null;
                    isCurrentRefresh = true;
                }
            }

            if (isCurrentRefresh)
            {
                IsLoading = false;
            }

            cancellationTokenSource.Dispose();
        }
    }

    private WindowEntry[] Reconcile(IReadOnlyList<Window> windows, CancellationToken cancellationToken)
    {
        Dictionary<WindowKey, WindowWalkerListItem> currentCache;
        lock (_cacheLock)
        {
            currentCache = _itemCache;
        }

        var nextCache = new Dictionary<WindowKey, WindowWalkerListItem>(windows.Count);
        var entries = new WindowEntry[windows.Count];
        for (var i = 0; i < windows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var window = windows[i];
            var key = new WindowKey(window.Hwnd, WindowProcess.GetProcessIDFromWindowHandle(window.Hwnd));
            if (!currentCache.TryGetValue(key, out var item))
            {
                item = ResultHelper.CreateResult(window);
            }
            else
            {
                ResultHelper.UpdateResult(item, window);
            }

            nextCache.Add(key, item);
            entries[i] = new WindowEntry(
                window,
                item,
                string.Equals(window.Process.Name, "explorer.exe", StringComparison.OrdinalIgnoreCase) && window.Process.IsShellProcess);
        }

        lock (_cacheLock)
        {
            _itemCache = nextCache;
        }

        return entries;
    }

    private void QueueIconLoads(IReadOnlyList<WindowWalkerListItem> items)
    {
        var needsIconLoad = false;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].NeedsIconLoad)
            {
                needsIconLoad = true;
                break;
            }
        }

        CancellationTokenSource? previousCancellationTokenSource;
        CancellationTokenSource? cancellationTokenSource = null;
        lock (_iconLoadLock)
        {
            previousCancellationTokenSource = _iconLoadCancellationTokenSource;
            if (needsIconLoad)
            {
                cancellationTokenSource = new CancellationTokenSource();
            }

            _iconLoadCancellationTokenSource = cancellationTokenSource;
        }

        previousCancellationTokenSource?.Cancel();
        if (cancellationTokenSource is null)
        {
            return;
        }

        _ = Task.Run(
            async () =>
            {
                var cancellationToken = cancellationTokenSource.Token;
                try
                {
                    // Avoid starting icon extraction for an intermediate query while the user is still typing.
                    await Task.Delay(IconLoadDelay, cancellationToken).ConfigureAwait(false);

                    // Keep this sequential. Each window message can block until its timeout, and issuing many
                    // requests concurrently makes the page compete with the applications it is enumerating.
                    for (var i = 0; i < items.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var item = items[i];
                        if (item.NeedsIconLoad)
                        {
                            item.LoadIcon();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    lock (_iconLoadLock)
                    {
                        if (ReferenceEquals(_iconLoadCancellationTokenSource, cancellationTokenSource))
                        {
                            _iconLoadCancellationTokenSource = null;
                        }
                    }

                    cancellationTokenSource.Dispose();
                }
            },
            CancellationToken.None);
    }

    private WindowWalkerListItem[] AddExplorerInfoIfNeeded(IReadOnlyList<WindowEntry> entries)
    {
        var addExplorerInfo = false;
        if (!SettingsManager.Instance.HideExplorerSettingInfo)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].IsShellExplorer)
                {
                    addExplorerInfo = true;
                    break;
                }
            }
        }

        var results = new WindowWalkerListItem[entries.Count + (addExplorerInfo ? 1 : 0)];
        var offset = 0;

        if (addExplorerInfo)
        {
            results[0] = _explorerInfoItem;
            offset = 1;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            results[i + offset] = entries[i].Item;
        }

        return results;
    }

    private static int ScoreFunction(string query, WindowEntry entry)
    {
        var titleScore = FuzzyStringMatcher.ScoreFuzzy(query, entry.Item.Title);
        var processNameScore = FuzzyStringMatcher.ScoreFuzzy(query, entry.Window.Process.Name ?? string.Empty);
        return Math.Max(titleScore, processNameScore);
    }

    private void SetLoadingComplete(CancellationTokenSource cancellationTokenSource)
    {
        bool isCurrentRefresh;
        lock (_refreshLock)
        {
            isCurrentRefresh = ReferenceEquals(_refreshCancellationTokenSource, cancellationTokenSource);
        }

        if (isCurrentRefresh)
        {
            IsLoading = false;
        }
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);

            lock (_refreshLock)
            {
                _refreshCancellationTokenSource?.Cancel();
                _refreshCancellationTokenSource = null;
            }

            lock (_iconLoadLock)
            {
                _iconLoadCancellationTokenSource?.Cancel();
                _iconLoadCancellationTokenSource = null;
            }
        }

        _disposed = true;
    }

    private readonly record struct WindowKey(IntPtr Hwnd, uint ProcessId);

    private sealed record WindowEntry(Window Window, WindowWalkerListItem Item, bool IsShellExplorer);
}
