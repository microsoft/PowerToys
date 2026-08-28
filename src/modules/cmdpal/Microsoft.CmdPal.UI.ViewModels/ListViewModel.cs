// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Dispatching;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListViewModel : PageViewModel, IDisposable
{
    public const int IncrementalRefresh = -2;

    private static readonly IEqualityComparer<IListItem> VmCacheComparer = new ProxyReferenceEqualityComparer();

    private readonly TaskFactory filterTaskFactory = new(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);

    private Dictionary<IListItem, ListItemViewModel> _vmCache = new(VmCacheComparer);

    // TODO: Do we want a base "ItemsPageViewModel" for anything that's going to have items?

    // Observable from MVVM Toolkit will auto create public properties that use INotifyPropertyChange change
    // https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observablegroupedcollections for grouping support
    public ObservableCollection<ListItemViewModel> FilteredItems { get; } = [];

    public FiltersViewModel? Filters { get; set; }

    private ObservableCollection<ListItemViewModel> Items { get; set; } = [];

    private readonly ExtensionObject<IListPage> _model;

    private readonly Lock _fetchStateLock = new();
    private readonly Lock _listLock = new();
    private readonly IContextMenuFactory _contextMenuFactory;

    // Background fetches alone take this lock. Selection, realization, and teardown
    // never acquire it, so installing a coordinator cannot block the UI thread.
    private readonly Lock _initializationCoordinatorLock = new();

    // Reentrancy guard for FilteredItems mutations. WinUI3's ListView processes
    // CollectionChanged synchronously, and its layout pass can pump the message
    // loop — which lets a second DoOnUiThread task start mutating FilteredItems
    // while the first is still mid-update. C# lock is reentrant (same thread
    // re-acquires), so _listLock cannot prevent this. Instead we use a boolean
    // flag and defer the latest update until the in-flight one finishes.
    private bool _isUpdatingFilteredItems;
    private Action? _pendingFilteredItemsUpdate;

    [ThreadStatic]
    private static Dictionary<ListViewModel, int>? _getItemsDepthByViewModel;

    private InterlockedBoolean _isLoadingMore;
    private int _activeFetchCount;
    private bool _deferredFetchRequested;
    private bool _deferredFetchKeepSelection = true;
    private bool _deferredFetchEnsureSelectionVisible;

    public event TypedEventHandler<ListViewModel, ItemsUpdatedEventArgs>? ItemsUpdated;

    public bool ShowEmptyContent =>
        IsInitialized &&
        FilteredItems.Count == 0 &&
        !IsFetching &&
        !IsLoading;

    public bool IsGridView { get; private set; }

    public IGridPropertiesViewModel? GridProperties { get; private set; }

    private bool IsFetching => Volatile.Read(ref _activeFetchCount) > 0;

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public bool ShowDetails { get; private set; }

    private string _modelPlaceholderText = string.Empty;

    public override string PlaceholderText => _modelPlaceholderText;

    public string SearchText { get; private set; } = string.Empty;

    public string InitialSearchText { get; private set; } = string.Empty;

    public CommandItemViewModel EmptyContent { get; private set; }

    public bool IsMainPage { get; init; }

    public bool IsTokenSearch { get; private set; }

    public bool HasCustomDebounceLogic => IsMainPage;

    private bool _isDynamic;

    private Task? _initializeItemsTask;
    private ListItemInitializationCoordinator? _itemInitializationCoordinator;

    // Navigation may suspend and later restore this VM from the Frame back stack.
    // Dispose/SafeCleanup are terminal; resumption must never undo either of them.
    private ListPageWorkState _workState = new(0, ListPageWorkStatus.Active, ListPageFetchPhase.Published);

    private bool IsWorkActive => Volatile.Read(ref _workState).Status == ListPageWorkStatus.Active;

    // For cancelling the task to load the properties from the items in the list
    private CancellationTokenSource? _cancellationTokenSource;

    // For cancelling the task for calling GetItems on the extension
    private CancellationTokenSource? _fetchItemsCancellationTokenSource;

    // For cancelling ongoing calls to update the extension's SearchText
    private CancellationTokenSource? filterCancellationTokenSource;

    private ListItemViewModel? _lastSelectedItem;

    // Persists across cancelled FetchItems calls so a forceFirstItem=true
    // intent is never lost when FetchItems(false) is cancelled by a
    // subsequent FetchItems(true).
    private volatile bool _forceFirstItemPending;

    // For cancelling a deferred SafeSlowInit when the user navigates rapidly
    private CancellationTokenSource? _selectedItemCts;

    public override bool IsInitialized
    {
        get => base.IsInitialized; protected set
        {
            base.IsInitialized = value;
            UpdateEmptyContent();
        }
    }

    public ListViewModel(IListPage model, TaskScheduler scheduler, AppExtensionHost host, ICommandProviderContext providerContext, IContextMenuFactory contextMenuFactory)
        : base(model, scheduler, host, providerContext)
    {
        _model = new(model);
        _contextMenuFactory = contextMenuFactory;
        EmptyContent = new(new(null), PageContext, contextMenuFactory: null);
    }

    private void FiltersPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FiltersViewModel.Filters))
        {
            var filtersViewModel = sender as FiltersViewModel;
            var hasFilters = filtersViewModel?.Filters.Length > 0;
            HasFilters = hasFilters;
            UpdateProperty(nameof(HasFilters));
        }
    }

    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args)
    {
        var isLoadingMore = _isLoadingMore.Value;

        // Perform a soft refresh when:
        // - the caller explicitly requests it through a flag piggybacked on args.TotalItems;
        // - incremental loading (LoadMore) is used, which implies a soft refresh by definition.
        RequestFetch(
            keepSelection: args.TotalItems == IncrementalRefresh || isLoadingMore,
            ensureSelectionVisible: !isLoadingMore);
    }

    protected override void OnSearchTextBoxUpdated(string searchTextBox)
    {
        // Dynamic pages will handler their own filtering. They will tell us if
        // something needs to change, by raising ItemsChanged.
        if (_isDynamic)
        {
            CancelAndDisposeTokenSource(ref filterCancellationTokenSource);
            var filterCts = filterCancellationTokenSource = new CancellationTokenSource();
            var filterToken = filterCts.Token;

            // Hop off to an exclusive scheduler background thread to update the
            // extension. We do this to ensure that all filter update requests
            // are serialized and in-order, so providers know to cancel previous
            // requests when a new one comes in. Otherwise, they may execute
            // concurrently.
            _ = filterTaskFactory.StartNew(
                () =>
                {
                    filterToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (_model.Unsafe is IDynamicListPage dynamic)
                        {
                            dynamic.SearchText = searchTextBox;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        ShowException(ex, _model?.Unsafe?.Name);
                    }
                },
                filterToken,
                TaskCreationOptions.None,
                filterTaskFactory.Scheduler!);
        }
        else
        {
            // But for all normal pages, we should run our fuzzy match on them.
            lock (_listLock)
            {
                RunFilteredItemsUpdate(ApplyFilterUnderLock);
            }

            ItemsUpdated?.Invoke(this, new ItemsUpdatedEventArgs(forceFirstItem: true, ensureSelectionVisible: true));
            UpdateEmptyContent();
            _isLoadingMore.Clear();
        }
    }

    public void UpdateCurrentFilter(string currentFilterId)
    {
        // We're getting called on the UI thread.
        // Hop off to a BG thread to update the extension.
        _ = Task.Run(() =>
        {
            try
            {
                if (_model.Unsafe is IListPage listPage)
                {
                    listPage.Filters?.CurrentFilterId = currentFilterId;
                }
            }
            catch (Exception ex)
            {
                ShowException(ex, _model?.Unsafe?.Name);
            }
        });
    }

    private void RequestFetch(bool keepSelection, bool ensureSelectionVisible)
    {
        if (DeferFetchWhileInactive(keepSelection, ensureSelectionVisible))
        {
            return;
        }

        // Keep RPC GetItems work off the UI thread. If the provider raises
        // ItemsChanged while we're already on a background thread, stay on that
        // thread so same-thread reentrancy detection still works.
        if (IsCurrentThreadUiThread())
        {
            QueueObservedBackgroundFetch(
                () => RequestFetch(keepSelection, ensureSelectionVisible),
                "Failed to request background fetch");
            return;
        }

        if (IsGetItemsActiveOnCurrentThread())
        {
            lock (_fetchStateLock)
            {
                _deferredFetchRequested = true;
                _deferredFetchKeepSelection &= keepSelection;
                _deferredFetchEnsureSelectionVisible |= ensureSelectionVisible;
            }

            return;
        }

        FetchItems(keepSelection, ensureSelectionVisible);
    }

    private void QueueDeferredFetchIfNeeded()
    {
        bool deferredFetchRequested;
        bool keepSelection;
        bool ensureSelectionVisible;
        lock (_fetchStateLock)
        {
            deferredFetchRequested = _deferredFetchRequested;
            keepSelection = _deferredFetchKeepSelection;
            ensureSelectionVisible = _deferredFetchEnsureSelectionVisible;
            _deferredFetchRequested = false;
            _deferredFetchKeepSelection = true;
            _deferredFetchEnsureSelectionVisible = false;
        }

        if (deferredFetchRequested)
        {
            QueueObservedBackgroundFetch(
                () => FetchItems(keepSelection, ensureSelectionVisible),
                "Failed to execute deferred fetch");
        }
    }

    private static Task QueueObservedBackgroundFetch(Action action, string logMessage)
    {
        return Task.Run(
            () =>
            {
                try
                {
                    action();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    CoreLogger.LogError(logMessage, ex);
                }
            });
    }

    //// Run on background thread, from InitializeAsync or Model_ItemsChanged
    private void FetchItems(bool keepSelection, bool ensureSelectionVisible, int? recoveryGeneration = null)
    {
        System.Diagnostics.Debug.Assert(!IsCurrentThreadUiThread(), "FetchItems should not run on the UI thread.");

        CancellationToken cancellationToken;
        int fetchGeneration;
        lock (_fetchStateLock)
        {
            if (!TryBeginFetch(keepSelection, ensureSelectionVisible, recoveryGeneration, out var work))
            {
                return;
            }

            fetchGeneration = work.Generation;
            if (!work.KeepSelection)
            {
                _forceFirstItemPending = true;
            }

            // Capture the token before publishing its owner: navigation can cancel
            // and dispose the source without acquiring this background fetch lock.
            var fetchCancellation = new CancellationTokenSource();
            cancellationToken = fetchCancellation.Token;
            var previousCancellation = Interlocked.Exchange(ref _fetchItemsCancellationTokenSource, fetchCancellation);
            CancelAndDisposeTokenSource(ref previousCancellation);
            if (!IsCurrentFetch(fetchGeneration))
            {
                if (Interlocked.CompareExchange(ref _fetchItemsCancellationTokenSource, null, fetchCancellation) == fetchCancellation)
                {
                    fetchCancellation.Dispose();
                }

                return;
            }
        }

        // Declared outside try so catch blocks can reference them
        List<ListItemViewModel> createdViewModels = [];
        var itemsTransferredToList = false;
        var fetchCountIncremented = false;

        try
        {
            fetchCountIncremented = true;
            if (Interlocked.Increment(ref _activeFetchCount) == 1)
            {
                UpdateEmptyContent();
            }

            ThrowIfFetchCanceledOrStale(fetchGeneration, cancellationToken);

            IListItem[] newItems;
            try
            {
                EnterGetItemsScope();
                newItems = _model.Unsafe!.GetItems();
            }
            finally
            {
                ExitGetItemsScope();
            }

            ThrowIfFetchCanceledOrStale(fetchGeneration, cancellationToken);

            // Collect all the items into new viewmodels
            List<ListItemViewModel> newViewModels = new(newItems.Length);
            var currentCache = ReadVmCache();
            var nextCache = new Dictionary<IListItem, ListItemViewModel>(newItems.Length, VmCacheComparer);
            var showsTitle = GridProperties?.ShowTitle ?? true;
            var showsSubtitle = GridProperties?.ShowSubtitle ?? true;
            var created = 0;
            var reused = 0;
            foreach (var item in newItems)
            {
                try
                {
                    if (item is null)
                    {
                        continue;
                    }

                    ThrowIfFetchCanceledOrStale(fetchGeneration, cancellationToken);

                    if (nextCache.TryGetValue(item, out var existing) || currentCache.TryGetValue(item, out existing))
                    {
                        existing.LayoutShowsTitle = showsTitle;
                        existing.LayoutShowsSubtitle = showsSubtitle;
                        newViewModels.Add(existing);
                        nextCache[item] = existing;
                        reused++;
                        continue;
                    }

                    var viewModel = new ListItemViewModel(item, new(this), _contextMenuFactory);

                    // If an item fails to load, silently ignore it.
                    if (viewModel.SafeFastInit())
                    {
                        viewModel.LayoutShowsTitle = showsTitle;
                        viewModel.LayoutShowsSubtitle = showsSubtitle;

                        newViewModels.Add(viewModel);
                        createdViewModels.Add(viewModel);
                        nextCache[item] = viewModel;
                        created++;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Our own stale/cancel checks throw OCE to stop the whole fetch
                    // promptly. Only swallow item-local cancellations.
                    ThrowIfFetchCanceledOrStale(fetchGeneration, cancellationToken);
                    CoreLogger.LogDebug("Item load cancelled during fetch");
                }
                catch (Exception ex)
                {
                    CoreLogger.LogError("Failed to load item:\n", ex);
                }
            }

#if DEBUG
            CoreLogger.LogInfo($"[ListViewModel] FetchItems: {created} created, {reused} reused, {nextCache.Count} cached");
#endif

            ThrowIfFetchCanceledOrStale(fetchGeneration, cancellationToken);

            var firstTwenty = newViewModels.Take(20);
            foreach (var item in firstTwenty)
            {
                ThrowIfFetchCanceledOrStale(fetchGeneration, cancellationToken);

                item?.InitializePropertiesOnce();
            }

            ThrowIfFetchCanceledOrStale(fetchGeneration, cancellationToken);

            List<ListItemViewModel> removedItems;
            lock (_fetchStateLock)
            {
                ThrowIfFetchCanceledOrStale(fetchGeneration, cancellationToken);

                lock (_listLock)
                {
                    ThrowIfFetchCanceledOrStale(fetchGeneration, cancellationToken);

                    // Now that we have new ViewModels for everything from the
                    // extension, smartly update our list of VMs
                    ListHelpers.InPlaceUpdateList(Items, newViewModels, out removedItems);

                    PublishVmCache(nextCache);

                    // DO NOT ThrowIfCancellationRequested AFTER THIS! If you do,
                    // you'll clean up list items that we've now transferred into
                    // .Items
                }
            }

            itemsTransferredToList = true;
            AdvanceFetchPhase(fetchGeneration, ListPageFetchPhase.Committed);

            // If we removed items, we need to clean them up, to remove our event handlers
            foreach (var removedItem in removedItems)
            {
                removedItem.SafeCleanup();
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected, don't treat as error

            // However, if we were cancelled, we didn't actually add these items to
            // our Items list. Before we release them to the GC, make sure we clean
            // them up
            if (!itemsTransferredToList)
            {
                foreach (var vm in createdViewModels)
                {
                    vm.SafeCleanup();
                }
            }

            return;
        }
        catch (Exception ex)
        {
            // TODO: Move this within the for loop, so we can catch issues with individual items
            // Create a special ListItemViewModel for errors and use an ItemTemplateSelector in the ListPage to display error items differently.
            if (!itemsTransferredToList)
            {
                foreach (var vm in createdViewModels)
                {
                    vm.SafeCleanup();
                }
            }

            ShowException(ex, _model?.Unsafe?.Name);
            throw;
        }
        finally
        {
            if (fetchCountIncremented && Interlocked.Decrement(ref _activeFetchCount) == 0)
            {
                UpdateEmptyContent();
            }
        }

        StartItemInitialization(fetchGeneration, cancellationToken);
        QueueItemsPublication(fetchGeneration);
    }

    private void QueueItemsPublication(int fetchGeneration)
    {
        DoOnUiThread(() =>
        {
            lock (_fetchStateLock)
            {
                if (!IsCurrentFetch(fetchGeneration))
                {
                    return;
                }

                lock (_listLock)
                {
                    // A deferred mutation is not a completed milestone. Keep its
                    // notification and phase advancement inside the guarded action.
                    RunFilteredItemsUpdate(() => PublishItemsUnderLock(fetchGeneration));
                }
            }
        });
    }

    private void PublishItemsUnderLock(int fetchGeneration)
    {
        // RunFilteredItemsUpdate's callers hold _listLock, including when a
        // reentrant publication is deferred until an earlier mutation finishes.
        var work = Volatile.Read(ref _workState);
        if (work.Status != ListPageWorkStatus.Active || work.Generation != fetchGeneration)
        {
            return;
        }

        // Reuse the same filtering/publication path after a fetch and when Back
        // recovers a committed snapshot whose callback was cancelled.
        if (!_isDynamic)
        {
            ApplyFilterUnderLock();
        }
        else
        {
            var snapshot = Items.Where(i => !i.IsInErrorState).ToList();
            ListHelpers.InPlaceUpdateList(FilteredItems, snapshot);
        }

        UpdateEmptyContent();
        if (!IsCurrentFetch(fetchGeneration))
        {
            return;
        }

        // Consume selection intent only when the retained snapshot reaches the
        // UI, including a request originally received while suspended.
        var forceFirst = _forceFirstItemPending || !work.KeepSelection;
        _forceFirstItemPending = false;

        ItemsUpdated?.Invoke(
            this,
            new ItemsUpdatedEventArgs(
                forceFirstItem: IsRootPage && forceFirst,
                ensureSelectionVisible: work.EnsureSelectionVisible));
        _isLoadingMore.Clear();
        AdvanceFetchPhase(fetchGeneration, ListPageFetchPhase.Published);
    }

    private void StartItemInitialization(int fetchGeneration, CancellationToken fetchCancellationToken)
    {
        System.Diagnostics.Debug.Assert(!IsCurrentThreadUiThread(), "Coordinator installation belongs to the background fetch.");

        lock (_initializationCoordinatorLock)
        {
            if (!IsCurrentFetch(fetchGeneration) || fetchCancellationToken.IsCancellationRequested)
            {
                return;
            }

            ListItemViewModel[] itemSnapshot;
            lock (_listLock)
            {
                itemSnapshot = Items.ToArray();
            }

            // Serialize only background installations. A superseded fetch must not
            // attach items to an older coordinator after a newer one was installed.
            var initializeItemsCts = new CancellationTokenSource();
            var initializeItemsToken = initializeItemsCts.Token;
            var coordinator = new ListItemInitializationCoordinator(itemSnapshot);
            var previousCoordinator = Interlocked.Exchange(ref _itemInitializationCoordinator, coordinator);
            var previousCancellation = Interlocked.Exchange(ref _cancellationTokenSource, initializeItemsCts);

            // The constructor above must reattach and replay live demand before Stop:
            // stopping (or a racing producer observing it) can discard queue entries
            // whose publishers already returned success. Item-owned demand survives
            // that discard only because the replacement has already replayed it.
            previousCoordinator?.Stop();
            CancelAndDisposeTokenSource(ref previousCancellation);

            // Navigation/teardown never wait for the background-only lock. Recheck
            // the generation too: a suspend/resume cycle must not revive this fetch.
            // A Stop after this check is also safe before Run starts.
            if (!IsCurrentFetch(fetchGeneration) || fetchCancellationToken.IsCancellationRequested)
            {
                coordinator.Stop();
                Interlocked.CompareExchange(ref _itemInitializationCoordinator, null, coordinator);
                if (Interlocked.CompareExchange(ref _cancellationTokenSource, null, initializeItemsCts) == initializeItemsCts)
                {
                    initializeItemsCts.Dispose();
                }

                return;
            }

            _initializeItemsTask = new Task(() => coordinator.Run(initializeItemsToken));
            _initializeItemsTask.Start();
        }
    }

    /// <summary>
    /// Apply our current filter text to the list of items, and update
    /// FilteredItems to match the results.
    /// </summary>
    private void ApplyFilterUnderLock() => ListHelpers.InPlaceUpdateList(FilteredItems, FilterList(Items, SearchTextBox));

    /// <summary>
    /// Executes an action that mutates <see cref="FilteredItems"/> with a
    /// reentrancy guard.  WinUI3's native XAML renderer can pump the
    /// message loop while processing a <c>CollectionChanged</c>
    /// notification, which allows a second queued UI-thread task to begin
    /// mutating the same collection before the first task finishes.  This
    /// causes heap corruption inside the native ItemsRepeater / ListView
    /// and manifests as an access-violation in ntdll.dll.
    ///
    /// The guard detects reentrancy (same UI thread re-entering) and
    /// stores only the <em>latest</em> pending action.  Once the
    /// in-flight mutation completes, the pending action (if any) executes
    /// immediately, ensuring the UI always converges to the newest state
    /// without overlapping mutations.
    /// </summary>
    private void RunFilteredItemsUpdate(Action updateAction)
    {
        if (_isUpdatingFilteredItems)
        {
            // Reentrant call — store only the latest; earlier stale
            // updates are intentionally dropped.
            _pendingFilteredItemsUpdate = updateAction;
            return;
        }

        _isUpdatingFilteredItems = true;
        try
        {
            updateAction();

            // Drain any update that was enqueued while we were running.
            while (_pendingFilteredItemsUpdate is not null)
            {
                var pending = _pendingFilteredItemsUpdate;
                _pendingFilteredItemsUpdate = null;
                pending();
            }
        }
        finally
        {
            _isUpdatingFilteredItems = false;
        }
    }

    private Dictionary<IListItem, ListItemViewModel> ReadVmCache() => Volatile.Read(ref _vmCache);

    private static bool IsCurrentThreadUiThread()
    {
        try
        {
            return DispatcherQueue.GetForCurrentThread()?.HasThreadAccess == true;
        }
        catch (COMException)
        {
            return false;
        }
    }

    /// <summary>
    /// Detects if we're currently within a GetItems call on this thread for this view model. This is used to detect
    /// reentrant calls to GetItems, so we can defer subsequent calls until the first one finishes, to avoid
    /// concurrent GetItems calls which most extensions won't be expecting.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if we're currently within a GetItems call on this thread for this view model; otherwise, <see langword="false"/>.
    /// </returns>
    private bool IsGetItemsActiveOnCurrentThread()
    {
        var depths = _getItemsDepthByViewModel;
        return depths is not null &&
               depths.TryGetValue(this, out var depth) &&
               depth > 0;
    }

    private void EnterGetItemsScope()
    {
        var depths = _getItemsDepthByViewModel ??= [];
        depths.TryGetValue(this, out var depth);
        depths[this] = depth + 1;
    }

    private void ExitGetItemsScope()
    {
        var depths = _getItemsDepthByViewModel;
        if (depths is null || !depths.TryGetValue(this, out var depth))
        {
            return;
        }

        if (depth == 1)
        {
            depths.Remove(this);
            if (depths.Count == 0)
            {
                _getItemsDepthByViewModel = null;
            }

            try
            {
                QueueDeferredFetchIfNeeded();
            }
            catch (Exception ex)
            {
                CoreLogger.LogError("Failed to queue deferred fetch", ex);
            }
        }
        else
        {
            depths[this] = depth - 1;
        }
    }

    private static void CancelAndDisposeTokenSource(ref CancellationTokenSource? tokenSource)
    {
        var tokenSourceToDispose = Interlocked.Exchange(ref tokenSource, null);
        if (tokenSourceToDispose is null)
        {
            return;
        }

        tokenSourceToDispose.Cancel();
        tokenSourceToDispose.Dispose();
    }

    private void ThrowIfFetchCanceledOrStale(int fetchGeneration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrentFetch(fetchGeneration))
        {
            throw new OperationCanceledException();
        }
    }

    private bool IsCurrentFetch(int fetchGeneration)
    {
        var work = Volatile.Read(ref _workState);
        return work.Status == ListPageWorkStatus.Active && work.Generation == fetchGeneration;
    }

    private void PublishVmCache(Dictionary<IListItem, ListItemViewModel> newCache)
    {
        Volatile.Write(ref _vmCache, newCache);
    }

    /// <summary>
    /// Helper to generate a weighting for a given list item, based on title,
    /// subtitle, etc. Largely a copy of the version in ListHelpers, but
    /// operating on ViewModels instead of extension objects.
    /// </summary>
    private static int ScoreListItem(string query, CommandItemViewModel listItem)
    {
        if (string.IsNullOrEmpty(query))
        {
            return 1;
        }

        var nameMatch = FuzzyStringMatcher.ScoreFuzzy(query, listItem.Title);
        var descriptionMatch = FuzzyStringMatcher.ScoreFuzzy(query, listItem.Subtitle);
        return new[] { nameMatch, (descriptionMatch - 4) / 2, 0 }.Max();
    }

    private struct ScoredListItemViewModel
    {
        public int Score;
        public ListItemViewModel ViewModel;
    }

    // Similarly stolen from ListHelpers.FilterList
    public static IEnumerable<ListItemViewModel> FilterList(IEnumerable<ListItemViewModel> items, string query)
    {
        var scores = items
            .Where(i => !i.IsInErrorState)
            .Select(li => new ScoredListItemViewModel() { ViewModel = li, Score = ScoreListItem(query, li) })
            .Where(score => score.Score > 0)
            .OrderByDescending(score => score.Score);
        return scores
            .Select(score => score.ViewModel);
    }

    // InvokeItemCommand is what this will be in Xaml due to source generator
    // This is what gets invoked when the user presses <enter>
    [RelayCommand]
    private void InvokeItem(ListItemViewModel? item)
    {
        if (item is not null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item.Command.Model, item.Model));
        }
        else if (ShowEmptyContent && EmptyContent.PrimaryCommand?.Model.Unsafe is not null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(
                EmptyContent.PrimaryCommand.Command.Model,
                EmptyContent.PrimaryCommand.Model));
        }
    }

    // This is what gets invoked when the user presses <ctrl+enter>
    [RelayCommand]
    private void InvokeSecondaryCommand(ListItemViewModel? item)
    {
        if (item is not null)
        {
            if (item.SecondaryCommand is not null)
            {
                WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item.SecondaryCommand.Command.Model, item.Model));
            }
        }
        else if (ShowEmptyContent && EmptyContent.SecondaryCommand?.Model.Unsafe is not null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(
                EmptyContent.SecondaryCommand.Command.Model,
                EmptyContent.SecondaryCommand.Model));
        }
    }

    [RelayCommand]
    private void UpdateSelectedItem(ListItemViewModel? item)
    {
        if (_lastSelectedItem is not null)
        {
            _lastSelectedItem.PropertyChanged -= SelectedItemPropertyChanged;
        }

        if (item is not null)
        {
            SetSelectedItem(item);
        }
        else
        {
            ClearSelectedItem();
        }
    }

    private void SetSelectedItem(ListItemViewModel item)
    {
        if (!IsWorkActive)
        {
            return;
        }

        _lastSelectedItem = item;
        _lastSelectedItem.PropertyChanged += SelectedItemPropertyChanged;

        WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(item));

        // Cancel any in-flight slow init from a previous selection and defer
        // the expensive work (extension IPC for MoreCommands, details) so
        // rapid arrow-key navigation skips intermediate items entirely.
        CancelAndDisposeTokenSource(ref _selectedItemCts);
        var cts = _selectedItemCts = new CancellationTokenSource();
        var ct = cts.Token;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    var initialized = await item.RequestInitializationAsync(ct).ConfigureAwait(false);

                    if (!initialized || ct.IsCancellationRequested)
                    {
                        if (!ct.IsCancellationRequested)
                        {
                            WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
                        }

                        return;
                    }

                    if (!item.SafeSlowInit())
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }

                        WeakReferenceMessenger.Default.Send<HideDetailsMessage>();

                        return;
                    }

                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    // SafeSlowInit completed on a background thread — details
                    // messages will be marshalled to the UI thread by the receiver.
                    if (ShowDetails && item.HasDetails)
                    {
                        WeakReferenceMessenger.Default.Send<ShowDetailsMessage>(new(item.Details));
                    }
                    else
                    {
                        WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
                    }

                    var suggestion = item.TextToSuggest;
                    DoOnUiThread(() =>
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }

                        TextToSuggest = suggestion;
                        WeakReferenceMessenger.Default.Send<UpdateSuggestionMessage>(new(suggestion));
                    });
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    CoreLogger.LogError("Failed to initialize the selected list item", ex);
                    if (!ct.IsCancellationRequested)
                    {
                        WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
                    }
                }
            },
            ct);
    }

    private void SelectedItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var item = _lastSelectedItem;
        if (item is null)
        {
            return;
        }

        // already on the UI thread here
        switch (e.PropertyName)
        {
            case nameof(item.Command):
            case nameof(item.SecondaryCommand):
            case nameof(item.AllCommands):
            case nameof(item.Name):
                WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(item));
                break;
            case nameof(item.Details):
                if (ShowDetails && item.HasDetails)
                {
                    WeakReferenceMessenger.Default.Send<ShowDetailsMessage>(new(item.Details));
                }
                else
                {
                    WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
                }

                break;
            case nameof(item.TextToSuggest):
                TextToSuggest = item.TextToSuggest;
                break;
        }
    }

    private void ClearSelectedItem()
    {
        CancelAndDisposeTokenSource(ref _selectedItemCts);

        WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(null));
        WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
        WeakReferenceMessenger.Default.Send<UpdateSuggestionMessage>(new(string.Empty));
        TextToSuggest = string.Empty;
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var model = _model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        _isDynamic = model is IDynamicListPage;

        IsGridView = model.GridProperties is not null;
        UpdateProperty(nameof(IsGridView));

        GridProperties = LoadGridPropertiesViewModel(model.GridProperties);
        GridProperties?.InitializeProperties();
        UpdateProperty(nameof(GridProperties));
        ApplyLayoutToItems();

        ShowDetails = model.ShowDetails;
        UpdateProperty(nameof(ShowDetails));

        _modelPlaceholderText = model.PlaceholderText;
        UpdateProperty(nameof(PlaceholderText));

        InitialSearchText = SearchText = model.SearchText;
        UpdateProperty(nameof(SearchText));
        UpdateProperty(nameof(InitialSearchText));

        EmptyContent = new(new(model.EmptyContent), PageContext, _contextMenuFactory);
        EmptyContent.SlowInitializeProperties();

        Filters?.PropertyChanged -= FiltersPropertyChanged;
        Filters = new(new(model.Filters), PageContext);
        Filters?.PropertyChanged += FiltersPropertyChanged;

        Filters?.InitializeProperties();
        UpdateProperty(nameof(Filters));

        if (model is IExtendedAttributesProvider haveProperties)
        {
            LoadExtendedAttributes(haveProperties.GetProperties().AsReadOnly());
        }

        FetchItems(keepSelection: true, ensureSelectionVisible: true);
        model.ItemsChanged += Model_ItemsChanged;
    }

    private static IGridPropertiesViewModel? LoadGridPropertiesViewModel(IGridProperties? gridProperties)
    {
        return gridProperties switch
        {
            IMediumGridLayout mediumGridLayout => new MediumGridPropertiesViewModel(mediumGridLayout),
            IGalleryGridLayout galleryGridLayout => new GalleryGridPropertiesViewModel(galleryGridLayout),
            ISmallGridLayout smallGridLayout => new SmallGridPropertiesViewModel(smallGridLayout),
            _ => null,
        };
    }

    private void LoadExtendedAttributes(IReadOnlyDictionary<string, object> properties)
    {
        // Check if this is a token page
        if (properties.TryGetValue("TokenSearch", out var isTokenSearchObj) &&
            isTokenSearchObj is bool isTokenSearch)
        {
            IsTokenSearch = isTokenSearch;
            UpdateProperty(nameof(IsTokenSearch));
        }
    }

    public void LoadMoreIfNeeded()
    {
        var model = _model.Unsafe;
        if (model is null)
        {
            return;
        }

        if (!_isLoadingMore.Set())
        {
            return;

            // NOTE: May miss newly available items until next scroll if model
            // state changes between our check and this reset
        }

        _ = Task.Run(() =>
        {
            // Execute all COM calls on background thread to avoid reentrancy issues with UI
            // with the UI thread when COM starts inner message pump
            try
            {
                if (model.HasMoreItems)
                {
                    model.LoadMore();

                    // LoadMore must raise ItemsChanged; the resulting fetch clears
                    // _isLoadingMore when the updated items are published.
                }
                else
                {
                    _isLoadingMore.Clear();
                }
            }
            catch (Exception ex)
            {
                _isLoadingMore.Clear();
                ShowException(ex, model.Name);
            }
        });
    }

    protected override void FetchProperty(string propertyName)
    {
        base.FetchProperty(propertyName);

        var model = _model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(GridProperties):
                IsGridView = model.GridProperties is not null;
                GridProperties = LoadGridPropertiesViewModel(model.GridProperties);
                GridProperties?.InitializeProperties();
                UpdateProperty(nameof(IsGridView));
                ApplyLayoutToItems();
                break;
            case nameof(ShowDetails):
                ShowDetails = model.ShowDetails;
                break;
            case nameof(PlaceholderText):
                _modelPlaceholderText = model.PlaceholderText;
                break;
            case nameof(SearchText):
                SearchText = model.SearchText;
                break;
            case nameof(EmptyContent):
                EmptyContent = new(new(model.EmptyContent), PageContext, contextMenuFactory: null);
                EmptyContent.SlowInitializeProperties();
                break;
            case nameof(Filters):
                Filters?.PropertyChanged -= FiltersPropertyChanged;
                Filters = new(new(model.Filters), PageContext);
                Filters?.PropertyChanged += FiltersPropertyChanged;
                Filters?.InitializeProperties();
                break;
            case nameof(IsLoading):
                UpdateEmptyContent();
                break;
        }

        UpdateProperty(propertyName);
    }

    private void UpdateEmptyContent()
    {
        UpdateProperty(nameof(ShowEmptyContent));
        if (!ShowEmptyContent || EmptyContent.Model.Unsafe is null)
        {
            return;
        }

        UpdateProperty(nameof(EmptyContent));

        DoOnUiThread(
           () =>
           {
               WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(EmptyContent));
           });
    }

    private void ApplyLayoutToItems()
    {
        lock (_listLock)
        {
            var showsTitle = GridProperties?.ShowTitle ?? true;
            var showsSubtitle = GridProperties?.ShowSubtitle ?? true;

            foreach (var item in Items)
            {
                item.LayoutShowsTitle = showsTitle;
                item.LayoutShowsSubtitle = showsSubtitle;
            }
        }
    }

    // The shell serializes navigation transitions on the UI thread. Terminal
    // cleanup may race them, but resumption can only transition Suspended -> Active.
    internal void SuspendForNavigation()
    {
        if (TryChangeWorkStatus(ListPageWorkStatus.Active, ListPageWorkStatus.Suspended) is not null)
        {
            CancelPendingWork();
        }
    }

    internal Task ResumeAfterNavigation()
    {
        var work = TryChangeWorkStatus(ListPageWorkStatus.Suspended, ListPageWorkStatus.Active);
        if (work is null)
        {
            return Task.CompletedTask;
        }

        // Do not consume the recovery record when queueing: another navigation
        // can invalidate this visit before the worker or UI callback ever runs.
        return QueueObservedBackgroundFetch(
            () =>
            {
                if (!IsCurrentFetch(work.Generation))
                {
                    return;
                }

                if (work.Phase == ListPageFetchPhase.Fetching)
                {
                    FetchItems(work.KeepSelection, work.EnsureSelectionVisible, work.Generation);
                }
                else
                {
                    StartItemInitialization(work.Generation, CancellationToken.None);
                    if (work.Phase == ListPageFetchPhase.Committed)
                    {
                        QueueItemsPublication(work.Generation);
                    }
                }
            },
            "Failed to resume list page after navigation");
    }

    private bool DeferFetchWhileInactive(bool keepSelection, bool ensureSelectionVisible)
    {
        while (true)
        {
            var work = Volatile.Read(ref _workState);
            if (work.Status == ListPageWorkStatus.Active)
            {
                return false;
            }

            if (work.Status == ListPageWorkStatus.Stopped)
            {
                return true;
            }

            var pendingKeepSelection = work.KeepSelection && keepSelection;
            var pendingEnsureSelectionVisible = work.EnsureSelectionVisible || ensureSelectionVisible;
            var pending = work.Phase == ListPageFetchPhase.Fetching &&
                work.KeepSelection == pendingKeepSelection &&
                work.EnsureSelectionVisible == pendingEnsureSelectionVisible
                    ? work
                    : work with
                    {
                        Phase = ListPageFetchPhase.Fetching,
                        KeepSelection = pendingKeepSelection,
                        EnsureSelectionVisible = pendingEnsureSelectionVisible,
                    };

            // Even an already-covered request verifies ownership with a no-op CAS:
            // if resume won, this request must retry on the now-active page.
            if (ReferenceEquals(Interlocked.CompareExchange(ref _workState, pending, work), work))
            {
                return true;
            }

            // Status and pending intent share one CAS. If resume won, retry sees
            // Active and the caller fetches normally; no late flag can be stranded.
        }
    }

    private bool TryBeginFetch(bool keepSelection, bool ensureSelectionVisible, int? recoveryGeneration, out ListPageWorkState work)
    {
        while (true)
        {
            var previous = Volatile.Read(ref _workState);
            work = previous;
            if (recoveryGeneration.HasValue && previous.Generation != recoveryGeneration.Value)
            {
                return false;
            }

            if (previous.Status != ListPageWorkStatus.Active)
            {
                if (DeferFetchWhileInactive(keepSelection, ensureSelectionVisible))
                {
                    return false;
                }

                continue;
            }

            work = new(
                unchecked(previous.Generation + 1),
                ListPageWorkStatus.Active,
                ListPageFetchPhase.Fetching,
                previous.KeepSelection && keepSelection,
                previous.EnsureSelectionVisible || ensureSelectionVisible);

            // Use ReferenceEquals for all work-state CAS results: record == can
            // mistake a distinct-but-equal snapshot for a successful exchange.
            if (ReferenceEquals(Interlocked.CompareExchange(ref _workState, work, previous), previous))
            {
                return true;
            }
        }
    }

    private void AdvanceFetchPhase(int generation, ListPageFetchPhase phase)
    {
        var work = Volatile.Read(ref _workState);
        if (work.Status != ListPageWorkStatus.Active || work.Generation != generation)
        {
            return;
        }

        var completed = work with
        {
            Phase = phase,
            KeepSelection = phase == ListPageFetchPhase.Published || work.KeepSelection,
            EnsureSelectionVisible = phase != ListPageFetchPhase.Published && work.EnsureSelectionVisible,
        };

        // A failed CAS means another fetch or navigation owns recovery now. Never
        // let a late unwind/commit/publication rewrite that owner's obligation.
        Interlocked.CompareExchange(ref _workState, completed, work);
    }

    private ListPageWorkState? TryChangeWorkStatus(ListPageWorkStatus from, ListPageWorkStatus to)
    {
        while (true)
        {
            var work = Volatile.Read(ref _workState);
            if (work.Status != from)
            {
                return null;
            }

            var next = work with { Status = to, Generation = unchecked(work.Generation + 1) };
            if (ReferenceEquals(Interlocked.CompareExchange(ref _workState, next, work), work))
            {
                return next;
            }
        }
    }

    private void CancelPendingWork()
    {
        // The status transition already invalidated callbacks and retained their
        // unfinished phase atomically. Never take worker-owned locks on navigation.
        CancelAndDisposeTokenSource(ref _selectedItemCts);
        CancelAndDisposeTokenSource(ref _cancellationTokenSource);
        Interlocked.Exchange(ref _itemInitializationCoordinator, null)?.Stop();
        CancelAndDisposeTokenSource(ref filterCancellationTokenSource);
        CancelAndDisposeTokenSource(ref _fetchItemsCancellationTokenSource);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        StopWork();
    }

    private void StopWork()
    {
        while (true)
        {
            var work = Volatile.Read(ref _workState);
            if (work.Status == ListPageWorkStatus.Stopped)
            {
                return;
            }

            if (TryChangeWorkStatus(work.Status, ListPageWorkStatus.Stopped) is not null)
            {
                break;
            }
        }

        CancelPendingWork();
    }

    protected override void UnsafeCleanup()
    {
        StopWork();
        base.UnsafeCleanup();

        EmptyContent?.SafeCleanup();
        EmptyContent = new(new(null), PageContext, contextMenuFactory: null); // necessary?

        lock (_listLock)
        {
            foreach (var item in Items)
            {
                item.SafeCleanup();
            }

            Items.Clear();
            RunFilteredItemsUpdate(() =>
            {
                foreach (var item in FilteredItems)
                {
                    item.SafeCleanup();
                }

                FilteredItems.Clear();
            });
        }

        PublishVmCache(new(VmCacheComparer));

        Filters?.PropertyChanged -= FiltersPropertyChanged;
        Filters?.SafeCleanup();

        var model = _model.Unsafe;
        if (model is not null)
        {
            model.ItemsChanged -= Model_ItemsChanged;
        }
    }

    private sealed class ProxyReferenceEqualityComparer : IEqualityComparer<IListItem>
    {
        public bool Equals(IListItem? x, IListItem? y) => ReferenceEquals(x, y);

        public int GetHashCode(IListItem obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
