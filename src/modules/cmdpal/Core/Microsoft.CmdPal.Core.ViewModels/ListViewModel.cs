// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.Common;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class ListViewModel : PageViewModel, IDisposable
{
    // private readonly HashSet<ListItemViewModel> _itemCache = [];
    private readonly TaskFactory filterTaskFactory = new(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);

    private readonly Dictionary<IListItem, ListItemViewModel> _vmCache =
        new(new ReferenceEqualityComparer<IListItem>());

    // TODO: Do we want a base "ItemsPageViewModel" for anything that's going to have items?

    // Observable from MVVM Toolkit will auto create public properties that use INotifyPropertyChange change
    // https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observablegroupedcollections for grouping support
    public ObservableCollection<ListItemViewModel> FilteredItems { get; } = [];

    public FiltersViewModel? Filters { get; set; }

    private ObservableCollection<ListItemViewModel> Items { get; set; } = [];

    private readonly ExtensionObject<IListPage> _model;

    private readonly Lock _listLock = new();

    private InterlockedBoolean _isLoading;
    private bool _isFetching;

    public event TypedEventHandler<ListViewModel, object>? ItemsUpdated;

    public bool ShowEmptyContent =>
        IsInitialized &&
        FilteredItems.Count == 0 &&
        (!_isFetching) &&
        IsLoading == false;

    public bool IsGridView => GridProperties.IsGrid;

    public IGridPropertiesViewModel GridProperties { get; private set; } = SinglelineListPropertiesViewModel.Default;

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public bool ShowDetails { get; private set; }

    private string _modelPlaceholderText = string.Empty;

    public override string PlaceholderText => _modelPlaceholderText;

    public string SearchText { get; private set; } = string.Empty;

    public string InitialSearchText { get; private set; } = string.Empty;

    public CommandItemViewModel EmptyContent { get; private set; }

    public bool IsMainPage { get; init; }

    private bool _isDynamic;

    private Task? _initializeItemsTask;

    // For cancelling the task to load the properties from the items in the list
    private CancellationTokenSource? _cancellationTokenSource;

    // For cancelling the task for calling GetItems on the extension
    private CancellationTokenSource? _fetchItemsCancellationTokenSource;

    // For cancelling ongoing calls to update the extension's SearchText
    private CancellationTokenSource? filterCancellationTokenSource;

    private ListItemViewModel? _lastSelectedItem;

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

    public ListViewModel(IListPage model, TaskScheduler scheduler, AppExtensionHost host)
        : base(model, scheduler, host)
    {
        _model = new(model);
        EmptyContent = new(new(null), PageContext);
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

    // TODO: Does this need to hop to a _different_ thread, so that we don't block the extension while we're fetching?
    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args) => FetchItems();

    protected override void OnSearchTextBoxUpdated(string searchTextBox)
    {
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

            ItemsUpdated?.Invoke(this, /* forceFirstItem */ true);
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
        // Cancel any previous FetchItems operation
        _fetchItemsCancellationTokenSource?.Cancel();
        _fetchItemsCancellationTokenSource?.Dispose();
        _fetchItemsCancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _fetchItemsCancellationTokenSource.Token;

        _isFetching = true;

        // Collect all the items into new viewmodels
        List<ListItemViewModel> newViewModels = [];

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

            var showsTitle = GridProperties?.ShowTitle ?? true;
            var showsSubtitle = GridProperties?.ShowSubtitle ?? true;
            var created = 0;
            var reused = 0;
            foreach (var item in newItems)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (_vmCache.TryGetValue(item, out var existing))
                {
                    existing.LayoutShowsTitle = showsTitle;
                    existing.LayoutShowsSubtitle = showsSubtitle;
                    newViewModels.Add(existing);
                    reused++;
                    continue;
                }

                var viewModel = new ListItemViewModel(item, new(this));
                if (viewModel.SafeFastInit())
                {
                    viewModel.LayoutShowsTitle = showsTitle;
                    viewModel.LayoutShowsSubtitle = showsSubtitle;

                    _vmCache[item] = viewModel;
                    newViewModels.Add(viewModel);
                    created++;
                }
            }

#if DEBUG
            CoreLogger.LogInfo($"[ListViewModel] FetchItems: {created} created, {reused} reused, {_vmCache.Count} cached");
#endif

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var firstTwenty = newViewModels.Take(20);
            foreach (var item in firstTwenty)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                item?.SafeInitializeProperties();
            }

            // Cancel any ongoing search
            _cancellationTokenSource?.Cancel();

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            List<ListItemViewModel> removedItems;
            lock (_listLock)
            {
                ListHelpers.InPlaceUpdateList(Items, newViewModels, out removedItems);

                _vmCache.Clear();
                foreach (var vm in newViewModels)
                {
                    if (vm.Model.Unsafe is { } li)
                    {
                        _vmCache[li] = vm;
                    }
                }
            }

            foreach (var removedItem in removedItems)
            {
                removedItem.SafeCleanup();
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var vm in newViewModels)
            {
                vm.SafeCleanup();
            }

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

        DoOnUiThread(
            () =>
            {
                lock (_listLock)
                {
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
                }

                ItemsUpdated?.Invoke(this, /* forceFirstItem */ !IsNested);
                _isLoading.Clear();
            });
    }

    private void InitializeItemsTask(CancellationToken ct)
    {
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

            item.SafeInitializeProperties();

            if (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Apply our current filter text to the list of items, and update
    /// FilteredItems to match the results.
    /// </summary>
    private void ApplyFilterUnderLock() => ListHelpers.InPlaceUpdateList(FilteredItems, FilterList(Items, SearchTextBox));

    private static int ScoreListItem(string query, CommandItemViewModel listItem)
    {
        if (string.IsNullOrEmpty(query))
        {
            return 1;
        }

        var nameMatch = FuzzyStringMatcher.ScoreFuzzy(query, listItem.Title);
        var descriptionMatch = FuzzyStringMatcher.ScoreFuzzy(query, listItem.Subtitle);
        return Math.Max(Math.Max(nameMatch, (descriptionMatch - 4) / 2), 0);
    }

    private struct ScoredListItemViewModel
    {
        public int Score;
        public ListItemViewModel ViewModel;
    }

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
        // All callers are UI-thread XAML event handlers (SelectionChanged,
        // ItemClick, RightTapped, Page_ItemsUpdated) so we can send messages
        // directly — no DoOnUiThread hop needed.
        _lastSelectedItem = item;
        _lastSelectedItem.PropertyChanged += SelectedItemPropertyChanged;

        // Immediately update the command bar and suggestion text — these use
        // already-initialized properties and must feel instant.
        WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(item));
        TextToSuggest = item.TextToSuggest;
        WeakReferenceMessenger.Default.Send<UpdateSuggestionMessage>(new(item.TextToSuggest));

        // Cancel any in-flight slow init from a previous selection and defer
        // the expensive work (extension IPC for MoreCommands, details) so
        // rapid arrow-key navigation skips intermediate items entirely.
        _selectedItemCts?.Cancel();
        var cts = _selectedItemCts = new CancellationTokenSource();

        // Capture the token before Task.Run — the CancellationToken struct
        // remains valid even if the CTS is cancelled/disposed by the next
        // selection change, avoiding ObjectDisposedException.
        var ct = cts.Token;

        _ = Task.Run(() =>
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (!item.SafeSlowInit())
            {
                if (!ct.IsCancellationRequested)
                {
                    WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
                }

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
        });
    }

    private void SelectedItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var item = _lastSelectedItem;
        if (item is null)
        {
            return;
        }

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
        // All callers are UI-thread paths (UpdateSelectedItem from XAML events).
        _selectedItemCts?.Cancel();

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

        GridProperties = LoadGridPropertiesViewModel(model.GridProperties);
        GridProperties?.InitializeProperties();
        UpdateProperty(nameof(GridProperties), nameof(IsGridView));
        ApplyLayoutToItems();

        ShowDetails = model.ShowDetails;
        UpdateProperty(nameof(ShowDetails));

        _modelPlaceholderText = model.PlaceholderText;
        UpdateProperty(nameof(PlaceholderText));

        InitialSearchText = SearchText = model.SearchText;
        UpdateProperty(nameof(SearchText));
        UpdateProperty(nameof(InitialSearchText));

        EmptyContent = new(new(model.EmptyContent), PageContext);
        EmptyContent.SlowInitializeProperties();

        Filters?.PropertyChanged -= FiltersPropertyChanged;
        Filters = new(new(model.Filters), PageContext);
        Filters?.PropertyChanged += FiltersPropertyChanged;

        Filters?.InitializeProperties();
        UpdateProperty(nameof(Filters));

        FetchItems();
        model.ItemsChanged += Model_ItemsChanged;
    }

    private static IGridPropertiesViewModel LoadGridPropertiesViewModel(IGridProperties? gridProperties)
    {
        return gridProperties switch
        {
            IMediumGridLayout mediumGridLayout => new MediumGridPropertiesViewModel(mediumGridLayout),
            IGalleryGridLayout galleryGridLayout => new GalleryGridPropertiesViewModel(galleryGridLayout),
            ISmallGridLayout smallGridLayout => new SmallGridPropertiesViewModel(smallGridLayout),
            ISinglelineListLayout layout => new SinglelineListPropertiesViewModel(layout),
            IMultilineListLayout layout => new MultiLineListPropertiesViewModel(layout),
            _ => SinglelineListPropertiesViewModel.Default,
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
        }

        _ = Task.Run(() =>
        {
            try
            {
                if (model.HasMoreItems)
                {
                    model.LoadMore();
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
                GridProperties = LoadGridPropertiesViewModel(model.GridProperties);
                GridProperties?.InitializeProperties();
                UpdateProperty(nameof(GridProperties));
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
                EmptyContent = new(new(model.EmptyContent), PageContext);
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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        filterCancellationTokenSource?.Cancel();
        filterCancellationTokenSource?.Dispose();
        filterCancellationTokenSource = null;

        _fetchItemsCancellationTokenSource?.Cancel();
        _fetchItemsCancellationTokenSource?.Dispose();
        _fetchItemsCancellationTokenSource = null;

        _selectedItemCts?.Cancel();
        _selectedItemCts?.Dispose();
        _selectedItemCts = null;
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        EmptyContent?.SafeCleanup();
        EmptyContent = new(new(null), PageContext); // necessary?

        _cancellationTokenSource?.Cancel();
        filterCancellationTokenSource?.Cancel();
        _fetchItemsCancellationTokenSource?.Cancel();
        _selectedItemCts?.Cancel();

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

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
