// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class ListViewModel : PageViewModel, IDisposable
{
    public event TypedEventHandler<ListViewModel, EventArgs>? SelectionChanged;

    private readonly Lock _listLock = new();

    private readonly ExtensionObject<IListPage> _model;

    private readonly ViewModelCache<IListItem, ListItemViewModel> _vmCache;
    private readonly Lock _fetchLock = new(); // serialize FetchItems to protect cache + Items

    private readonly TaskFactory filterTaskFactory = new(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);

    // For cancelling the task to load the properties from the items in the list
    private CancellationTokenSource? _cancellationTokenSource;

    // For cancelling the task for calling GetItems on the extension
    private CancellationTokenSource? _fetchItemsCancellationTokenSource;

    private Task? _initializeItemsTask;

    private bool _isDynamic;
    private bool _isFetching;

    private InterlockedBoolean _isLoading;

    private ListItemViewModel? _lastSelectedItem;

    private string _modelPlaceholderText = string.Empty;

    // For cancelling ongoing calls to update the extension's SearchText
    private CancellationTokenSource? filterCancellationTokenSource;
    private bool _stickyTopSelection = true;
    private bool _suppressSelectionChanged = DateTime.UtcNow.Second == 77;

    public ListItemViewModel? SelectedItem => _lastSelectedItem;

    public ListViewModel(IListPage model, TaskScheduler scheduler, AppExtensionHost host)
        : base(model, scheduler, host)
    {
        _model = new ExtensionObject<IListPage>(model);
        EmptyContent = new CommandItemViewModel(new ExtensionObject<ICommandItem>(null), PageContext);

        _vmCache = new ViewModelCache<IListItem, ListItemViewModel>(
            keys: new DefaultCacheKeyProvider<IListItem>(),
            factory: item => new ListItemViewModel(item, new(this)),
            rebind: (vm, item) =>
            {
                vm.Rebind(new ExtensionObject<IListItem>(item));
            },
            onEvict: vm => vm.SafeCleanup());
    }

    // TODO: Do we want a base "ItemsPageViewModel" for anything that's going to have items?

    // Observable from MVVM Toolkit will auto create public properties that use INotifyPropertyChange change
    // https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observablegroupedcollections for grouping support
    public ObservableCollection<ListItemViewModel> FilteredItems { get; } = [];

    public FiltersViewModel? Filters { get; set; }

    private ObservableCollection<ListItemViewModel> Items { get; } = [];

    public bool ShowEmptyContent =>
        IsInitialized &&
        FilteredItems.Count == 0 &&
        !_isFetching &&
        !IsLoading;

    public bool IsGridView { get; private set; }

    public IGridPropertiesViewModel? GridProperties { get; private set; }

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public bool ShowDetails { get; private set; }

    public override string PlaceholderText => _modelPlaceholderText;

    public string SearchText { get; private set; } = string.Empty;

    public string InitialSearchText { get; private set; } = string.Empty;

    public CommandItemViewModel EmptyContent { get; private set; }

    public bool IsMainPage { get; init; }

    public override bool IsInitialized
    {
        get => base.IsInitialized;
        protected set
        {
            base.IsInitialized = value;
            UpdateEmptyContent();
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _vmCache.Clear();

        filterCancellationTokenSource?.Cancel();
        filterCancellationTokenSource?.Dispose();
        filterCancellationTokenSource = null;

        _fetchItemsCancellationTokenSource?.Cancel();
        _fetchItemsCancellationTokenSource?.Dispose();
        _fetchItemsCancellationTokenSource = null;
    }

    public event TypedEventHandler<ListViewModel, object>? ItemsUpdated;

    private void FiltersPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FiltersViewModel.Filters))
        {
            var filtersViewModel = sender as FiltersViewModel;
            var hasFilters = filtersViewModel?.Filters.Length > 0;
            HasFilters = hasFilters;
            UpdateProperty(nameof(HasFilters));
        }
    }

    // TODO: Does this need to hop to a _different_ thread, so that we don't block the extension while we're fetching?
    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args) => FetchItems();

    protected override void OnSearchTextBoxUpdated(string searchTextBox)
    {
        _stickyTopSelection = true;
        _lastSelectedItem = null;

        //// TODO: Just temp testing, need to think about where we want to filter, as AdvancedCollectionView in View could be done, but then grouping need CollectionViewSource, maybe we do grouping in view
        //// and manage filtering below, but we should be smarter about this and understand caching and other requirements...
        //// Investigate if we re-use src\modules\cmdpal\extensionsdk\Microsoft.CommandPalette.Extensions.Toolkit\ListHelpers.cs InPlaceUpdateList and FilterList?

        // Dynamic pages will handler their own filtering. They will tell us if
        // something needs to change, by raising ItemsChanged.
        if (_isDynamic)
        {
            filterCancellationTokenSource?.Cancel();
            filterCancellationTokenSource?.Dispose();
            filterCancellationTokenSource = new CancellationTokenSource();

            // Hop off to an exclusive scheduler background thread to update the
            // extension. We do this to ensure that all filter update requests
            // are serialized and in-order, so providers know to cancel previous
            // requests when a new one comes in. Otherwise, they may execute
            // concurrently.
            _ = filterTaskFactory.StartNew(
                () =>
                {
                    filterCancellationTokenSource.Token.ThrowIfCancellationRequested();

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
                filterCancellationTokenSource.Token,
                TaskCreationOptions.None,
                filterTaskFactory.Scheduler!);
        }
        else
        {
            // But for all normal pages, we should run our fuzzy match on them.
            lock (_listLock)
            {
                ApplyFilterUnderLock();
            }

            ItemsUpdated?.Invoke(this, EventArgs.Empty);
            UpdateEmptyContent();
            _isLoading.Clear();
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

    //// Run on background thread, from InitializeAsync or Model_ItemsChanged
    private void FetchItems()
    {
        lock (_fetchLock)
        {
            _fetchItemsCancellationTokenSource?.Cancel();
            _fetchItemsCancellationTokenSource?.Dispose();
            _fetchItemsCancellationTokenSource = new CancellationTokenSource();

            var cancellationToken = _fetchItemsCancellationTokenSource.Token;
            _isFetching = true;

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var newItems = _model.Unsafe!.GetItems();

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _vmCache.BeginGeneration();

                Collection<ListItemViewModel> newViewModels = [];

                var showsTitle = GridProperties?.ShowTitle ?? true;
                var showsSubtitle = GridProperties?.ShowSubtitle ?? true;

                foreach (var item in newItems)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    // Reuse VMs across refreshes.
                    var vm = _vmCache.GetOrCreate(item, out var created);

                    // If this is a newly created VM, we should validate it.
                    if (created && !vm.SafeFastInit())
                    {
                        // Important: remove it from cache since it failed to init.
                        _vmCache.Remove(item);
                        continue;
                    }

                    vm.LayoutShowsTitle = showsTitle;
                    vm.LayoutShowsSubtitle = showsSubtitle;

                    // If you previously used SafeFastInit as a filter, keep it:
                    if (!created)
                    {
                        // Existing VMs should already be fast-inited, but if that assumption isn't true:
                        vm.SafeFastInit();
                    }

                    newViewModels.Add(vm);
                }

                // Initialize first twenty items (same as you do today)
                foreach (var item in newViewModels.Take(20))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    item.SafeInitializeProperties();
                }

                _cancellationTokenSource?.Cancel();

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Update Items in-place
                List<ListItemViewModel> removedFromItems = [];
                lock (_listLock)
                {
                    ListHelpers.InPlaceUpdateList(Items, newViewModels, out removedFromItems);
                }

                // KEY CHANGE:
                // removedFromItems are "not in the current subset", NOT "dead forever".
                // Do NOT SafeCleanup them here, or you'll churn on every incremental search step.
                // We'll cleanup only on cache eviction.

                // Evict only truly cold items. Tune this.
                _vmCache.EvictNotSeenFor(keepGenerations: 30);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                ShowException(ex, _model?.Unsafe?.Name);
                throw;
            }
            finally
            {
                _isFetching = false;
            }

            _cancellationTokenSource = new CancellationTokenSource();

            _initializeItemsTask = new Task(() =>
            {
                InitializeItemsTask(_cancellationTokenSource.Token);
            });
            _initializeItemsTask.Start();

            DoOnUiThread(() =>
            {
                lock (_listLock)
                {
                    // Now that our Items contains everything we want, it's time for us to
                    // re-evaluate our Filter on those items.
                    if (!_isDynamic)
                    {
                        // A static list? Great! Just run the filter.
                        ApplyFilterUnderLock();
                    }
                    else
                    {
                        // A dynamic list? Even better! Just stick everything into
                        // FilteredItems. The extension already did any filtering it cared about.
                        ListHelpers.InPlaceUpdateList(FilteredItems, Items.Where(i => !i.IsInErrorState).ToList());
                    }

                    UpdateEmptyContent();
                }

                RestoreSelectionAfterFilterUpdate();

                ItemsUpdated?.Invoke(this, EventArgs.Empty);
                _isLoading.Clear();
            });
        }
    }

    private void InitializeItemsTask(CancellationToken ct)
    {
        // Were we already canceled?
        if (ct.IsCancellationRequested)
        {
            return;
        }

        ListItemViewModel[] iterable;
        lock (_listLock)
        {
            iterable = Items.ToArray();
        }

        foreach (var item in iterable)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            // TODO: GH #502
            // We should probably remove the item from the list if it
            // entered the error state. I had issues doing that without having
            // multiple threads muck with `Items` (and possibly FilteredItems!)
            // at once.
            item.SafeInitializeProperties();

            if (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>
    ///     Apply our current filter text to the list of items, and update
    ///     FilteredItems to match the results.
    /// </summary>
    private void ApplyFilterUnderLock()
    {
        // Was the selected item currently the first selectable?
        var wasSelectedFirstSelectable = false;
        var currentSelected = _lastSelectedItem;

        if (currentSelected is not null)
        {
            ListItemViewModel? currentFirstSelectable = null;
            foreach (var vm in FilteredItems)
            {
                if (!vm.IsSectionOrSeparator)
                {
                    currentFirstSelectable = vm;
                    break;
                }
            }

            wasSelectedFirstSelectable = ReferenceEquals(currentFirstSelectable, currentSelected);
        }

        var next = FilterList(Items, SearchTextBox).ToList();

        if (wasSelectedFirstSelectable)
        {
            PinSelectedToFirstSelectableIfNeeded(next, currentSelected);
        }

        ListHelpers.InPlaceUpdateList(FilteredItems, next);
    }

    /// <summary>
    ///     Helper to generate a weighting for a given list item, based on title,
    ///     subtitle, etc. Largely a copy of the version in ListHelpers, but
    ///     operating on ViewModels instead of extension objects.
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

    // Similarly stolen from ListHelpers.FilterList
    public static IEnumerable<ListItemViewModel> FilterList(IEnumerable<ListItemViewModel> items, string query)
    {
        var scores = items
            .Where(i => !i.IsInErrorState)
            .Select(li => new ScoredListItemViewModel { ViewModel = li, Score = ScoreListItem(query, li) })
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
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(
                new PerformCommandMessage(item.Command.Model, item.Model));
        }
        else if (ShowEmptyContent && EmptyContent.PrimaryCommand?.Model.Unsafe is not null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new PerformCommandMessage(
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
                WeakReferenceMessenger.Default.Send<PerformCommandMessage>(
                    new PerformCommandMessage(item.SecondaryCommand.Command.Model, item.Model));
            }
        }
        else if (ShowEmptyContent && EmptyContent.SecondaryCommand?.Model.Unsafe is not null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new PerformCommandMessage(
                EmptyContent.SecondaryCommand.Command.Model,
                EmptyContent.SecondaryCommand.Model));
        }
    }

    [RelayCommand]
    private void UpdateSelectedItem(ListItemViewModel? item)
    {
        if (_suppressSelectionChanged)
        {
            return;
        }

        // Sticky iff the user is sitting on the first selectable item *right now*.
        // (When user navigates away, it becomes false; when they come back to top, it becomes true.)
        var first = FindFirstSelectable();
        _stickyTopSelection = (item is not null) && ReferenceEquals(item, first);

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
        if (!item.SafeSlowInit())
        {
            // Even if initialization fails, we need to hide any previously shown details
            DoOnUiThread(() =>
            {
                WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
            });
            return;
        }

        // GH #322:
        // For inexplicable reasons, if you try updating the command bar and
        // the details on the same UI thread tick as updating the list, we'll
        // explode
        DoOnUiThread(() =>
        {
            WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new UpdateCommandBarMessage(item));

            if (ShowDetails && item.HasDetails)
            {
                WeakReferenceMessenger.Default.Send<ShowDetailsMessage>(new ShowDetailsMessage(item.Details));
            }
            else
            {
                WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
            }

            TextToSuggest = item.TextToSuggest;
            WeakReferenceMessenger.Default.Send<UpdateSuggestionMessage>(
                new UpdateSuggestionMessage(item.TextToSuggest));
        });

        _lastSelectedItem = item;
        _lastSelectedItem.PropertyChanged += SelectedItemPropertyChanged;
    }

    private void SelectedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
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
                WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new UpdateCommandBarMessage(item));
                break;
            case nameof(item.Details):
                if (ShowDetails && item.HasDetails)
                {
                    WeakReferenceMessenger.Default.Send<ShowDetailsMessage>(new ShowDetailsMessage(item.Details));
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
        // GH #322:
        // For inexplicable reasons, if you try updating the command bar and
        // the details on the same UI thread tick as updating the list, we'll
        // explode
        DoOnUiThread(() =>
        {
            WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new UpdateCommandBarMessage(null));

            WeakReferenceMessenger.Default.Send<HideDetailsMessage>();

            WeakReferenceMessenger.Default.Send<UpdateSuggestionMessage>(new UpdateSuggestionMessage(string.Empty));

            TextToSuggest = string.Empty;
        });
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

        EmptyContent = new CommandItemViewModel(new ExtensionObject<ICommandItem>(model.EmptyContent), PageContext);
        EmptyContent.SlowInitializeProperties();

        Filters?.PropertyChanged -= FiltersPropertyChanged;
        Filters = new FiltersViewModel(new ExtensionObject<IFilters>(model.Filters), PageContext);
        Filters?.PropertyChanged += FiltersPropertyChanged;

        Filters?.InitializeProperties();
        UpdateProperty(nameof(Filters));

        FetchItems();
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

    public void LoadMoreIfNeeded()
    {
        var model = _model.Unsafe;
        if (model is null)
        {
            return;
        }

        if (!_isLoading.Set())
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

                    // _isLoading flag will be set as a result of LoadMore,
                    // which must raise ItemsChanged to end the loading.
                }
                else
                {
                    _isLoading.Clear();
                }
            }
            catch (Exception ex)
            {
                _isLoading.Clear();
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
                EmptyContent =
                    new CommandItemViewModel(new ExtensionObject<ICommandItem>(model.EmptyContent), PageContext);
                EmptyContent.SlowInitializeProperties();
                break;
            case nameof(Filters):
                Filters?.PropertyChanged -= FiltersPropertyChanged;
                Filters = new FiltersViewModel(new ExtensionObject<IFilters>(model.Filters), PageContext);
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

        DoOnUiThread(() =>
        {
            WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new UpdateCommandBarMessage(EmptyContent));
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

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        EmptyContent?.SafeCleanup();
        EmptyContent = new CommandItemViewModel(new ExtensionObject<ICommandItem>(null), PageContext); // necessary?

        _cancellationTokenSource?.Cancel();
        filterCancellationTokenSource?.Cancel();
        _fetchItemsCancellationTokenSource?.Cancel();

        lock (_listLock)
        {
            foreach (var item in Items)
            {
                item.SafeCleanup();
            }

            Items.Clear();
            foreach (var item in FilteredItems)
            {
                item.SafeCleanup();
            }

            FilteredItems.Clear();
        }

        Filters?.PropertyChanged -= FiltersPropertyChanged;
        Filters?.SafeCleanup();

        var model = _model.Unsafe;
        if (model is not null)
        {
            model.ItemsChanged -= Model_ItemsChanged;
        }
    }

    private ListItemViewModel? FindFirstSelectable()
    {
        foreach (var vm in FilteredItems)
        {
            if (!vm.IsSectionOrSeparator)
            {
                return vm;
            }
        }

        return null;
    }

    private void RestoreSelectionAfterFilterUpdate()
    {
        SetSelectedItem(FilteredItems.FirstOrDefault()!);
        SelectionChanged?.Invoke(this, EventArgs.Empty);

        /*
        if (!_stickyTopSelection)
        {
            // User navigated somewhere else; don't auto-jump unless selection is gone.
            // If selection is gone, pick first selectable as a fallback.
        }

        var candidate = FindFirstSelectable();

        if (candidate is null)
        {
            return;
        }

        if (ReferenceEquals(candidate, _lastSelectedItem))
        {
            return; // already selected, avoid re-triggering visuals
        }

        // IMPORTANT: do this on UI thread, and avoid re-entrancy into UpdateSelectedItem
        _suppressSelectionChanged = true;
        try
        {
            // Your view should bind ListView.SelectedItem -> ListViewModel.SelectedItem (or call a messenger).
            // If you don't have a SelectedItem property, you can call SetSelectedItem directly.
            SetSelectedItem(candidate);
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
        */
    }

    private static void PinSelectedToFirstSelectableIfNeeded(
        List<ListItemViewModel> list,
        ListItemViewModel? selected)
    {
        if (selected is null)
        {
            return;
        }

        // Selected must still exist in the new results and be selectable.
        if (selected.IsSectionOrSeparator)
        {
            return;
        }

        var selectedIndex = list.IndexOf(selected);
        if (selectedIndex < 0)
        {
            return;
        }

        // Find where the "first selectable" slot is in THIS list (skips headers/separators).
        var firstSelectableIndex = -1;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].IsSectionOrSeparator == false)
            {
                firstSelectableIndex = i;
                break;
            }
        }

        if (firstSelectableIndex < 0)
        {
            return;
        }

        // Only pin if the selected item was already the first selectable item previously.
        // (This avoids overriding user navigation away from the top.)
        // We'll decide that from the current FilteredItems before we rebuild it.
        // So this helper assumes you've already checked that condition.
        if (selectedIndex == firstSelectableIndex)
        {
            return; // already pinned
        }

        // Move the selected item into the first selectable slot.
        list.RemoveAt(selectedIndex);

        // If we removed an item before the insertion point, insertion index shifts by -1.
        if (selectedIndex < firstSelectableIndex)
        {
            firstSelectableIndex--;
        }

        list.Insert(firstSelectableIndex, selected);
    }

    private struct ScoredListItemViewModel
    {
        public int Score;
        public ListItemViewModel ViewModel;
    }
}
