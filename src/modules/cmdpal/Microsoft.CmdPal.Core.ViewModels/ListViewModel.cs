// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class ListViewModel : PageViewModel, IDisposable
{
    // private readonly HashSet<ListItemViewModel> _itemCache = [];

    // TODO: Do we want a base "ItemsPageViewModel" for anything that's going to have items?

    // Observable from MVVM Toolkit will auto create public properties that use INotifyPropertyChange change
    // https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observablegroupedcollections for grouping support
    [ObservableProperty]
    public partial ObservableCollection<ListItemViewModel> FilteredItems { get; set; } = [];

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

    public bool IsGridView { get; private set; }

    public IGridPropertiesViewModel? GridProperties { get; private set; }

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
    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationTokenSource? _fetchItemsCancellationTokenSource;

    private ListItemViewModel? _lastSelectedItem;

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

    // TODO: Does this need to hop to a _different_ thread, so that we don't block the extension while we're fetching?
    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args) => FetchItems();

    protected override void OnSearchTextBoxUpdated(string searchTextBox)
    {
        //// TODO: Just temp testing, need to think about where we want to filter, as AdvancedCollectionView in View could be done, but then grouping need CollectionViewSource, maybe we do grouping in view
        //// and manage filtering below, but we should be smarter about this and understand caching and other requirements...
        //// Investigate if we re-use src\modules\cmdpal\extensionsdk\Microsoft.CommandPalette.Extensions.Toolkit\ListHelpers.cs InPlaceUpdateList and FilterList?

        // Dynamic pages will handler their own filtering. They will tell us if
        // something needs to change, by raising ItemsChanged.
        if (_isDynamic)
        {
            // We're getting called on the UI thread.
            // Hop off to a BG thread to update the extension.
            _ = Task.Run(() =>
            {
                try
                {
                    if (_model.Unsafe is IDynamicListPage dynamic)
                    {
                        dynamic.SearchText = searchTextBox;
                    }
                }
                catch (Exception ex)
                {
                    ShowException(ex, _model?.Unsafe?.Name);
                }
            });
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
        // Cancel any previous FetchItems operation
        _fetchItemsCancellationTokenSource?.Cancel();
        _fetchItemsCancellationTokenSource?.Dispose();
        _fetchItemsCancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _fetchItemsCancellationTokenSource.Token;

        // TEMPORARY: just plop all the items into a single group
        // see 9806fe5d8 for the last commit that had this with sections
        _isFetching = true;

        // Collect all the items into new viewmodels
        Collection<ListItemViewModel> newViewModels = [];

        try
        {
            // Check for cancellation before starting expensive operations
            cancellationToken.ThrowIfCancellationRequested();

            var newItems = _model.Unsafe!.GetItems();

            // Check for cancellation after getting items from extension
            cancellationToken.ThrowIfCancellationRequested();

            // TODO we can probably further optimize this by also keeping a
            // HashSet of every ExtensionObject we currently have, and only
            // building new viewmodels for the ones we haven't already built.
            foreach (var item in newItems)
            {
                // Check for cancellation during item processing
                cancellationToken.ThrowIfCancellationRequested();

                ListItemViewModel viewModel = new(item, new(this));

                // If an item fails to load, silently ignore it.
                if (viewModel.SafeFastInit())
                {
                    newViewModels.Add(viewModel);
                }
            }

            // Check for cancellation before initializing first twenty items
            cancellationToken.ThrowIfCancellationRequested();

            var firstTwenty = newViewModels.Take(20);
            foreach (var item in firstTwenty)
            {
                cancellationToken.ThrowIfCancellationRequested();
                item?.SafeInitializeProperties();
            }

            // Cancel any ongoing search
            _cancellationTokenSource?.Cancel();

            // Check for cancellation before updating the list
            cancellationToken.ThrowIfCancellationRequested();

            List<ListItemViewModel> removedItems = [];
            lock (_listLock)
            {
                // Now that we have new ViewModels for everything from the
                // extension, smartly update our list of VMs
                ListHelpers.InPlaceUpdateList(Items, newViewModels, out removedItems);

                // DO NOT ThrowIfCancellationRequested AFTER THIS! If you do,
                // you'll clean up list items that we've now transferred into
                // .Items
            }

            // If we removed items, we need to clean them up, to remove our event handlers
            foreach (var removedItem in removedItems)
            {
                removedItem.SafeCleanup();
            }

            // TODO: Iterate over everything in Items, and prune items from the
            // cache if we don't need them anymore
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected, don't treat as error

            // However, if we were cancelled, we didn't actually add these items to
            // our Items list. Before we release them to the GC, make sure we clean
            // them up
            foreach (var vm in newViewModels)
            {
                vm.SafeCleanup();
            }

            return;
        }
        catch (Exception ex)
        {
            // TODO: Move this within the for loop, so we can catch issues with individual items
            // Create a special ListItemViewModel for errors and use an ItemTemplateSelector in the ListPage to display error items differently.
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
            try
            {
                InitializeItemsTask(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
            }
        });
        _initializeItemsTask.Start();

        DoOnUiThread(
            () =>
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
                        ListHelpers.InPlaceUpdateList(FilteredItems, Items.Where(i => !i.IsInErrorState));
                    }

                    UpdateEmptyContent();
                }

                ItemsUpdated?.Invoke(this, EventArgs.Empty);
                _isLoading.Clear();
            });
    }

    private void InitializeItemsTask(CancellationToken ct)
    {
        // Were we already canceled?
        ct.ThrowIfCancellationRequested();

        ListItemViewModel[] iterable;
        lock (_listLock)
        {
            iterable = Items.ToArray();
        }

        foreach (var item in iterable)
        {
            ct.ThrowIfCancellationRequested();

            // TODO: GH #502
            // We should probably remove the item from the list if it
            // entered the error state. I had issues doing that without having
            // multiple threads muck with `Items` (and possibly FilteredItems!)
            // at once.
            item.SafeInitializeProperties();

            ct.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Apply our current filter text to the list of items, and update
    /// FilteredItems to match the results.
    /// </summary>
    private void ApplyFilterUnderLock() => ListHelpers.InPlaceUpdateList(FilteredItems, FilterList(Items, SearchTextBox));

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

        var nameMatch = StringMatcher.FuzzySearch(query, listItem.Title);
        var descriptionMatch = StringMatcher.FuzzySearch(query, listItem.Subtitle);
        return new[] { nameMatch.Score, (descriptionMatch.Score - 4) / 2, 0 }.Max();
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
        if (!item.SafeSlowInit())
        {
            return;
        }

        // GH #322:
        // For inexplicable reasons, if you try updating the command bar and
        // the details on the same UI thread tick as updating the list, we'll
        // explode
        DoOnUiThread(
           () =>
           {
               WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(item));

               if (ShowDetails && item.HasDetails)
               {
                   WeakReferenceMessenger.Default.Send<ShowDetailsMessage>(new(item.Details));
               }
               else
               {
                   WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
               }

               TextToSuggest = item.TextToSuggest;
               WeakReferenceMessenger.Default.Send<UpdateSuggestionMessage>(new(item.TextToSuggest));
           });

        _lastSelectedItem = item;
        _lastSelectedItem.PropertyChanged += SelectedItemPropertyChanged;
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
        // GH #322:
        // For inexplicable reasons, if you try updating the command bar and
        // the details on the same UI thread tick as updating the list, we'll
        // explode
        DoOnUiThread(
           () =>
           {
               WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(null));

               WeakReferenceMessenger.Default.Send<HideDetailsMessage>();

               WeakReferenceMessenger.Default.Send<UpdateSuggestionMessage>(new(string.Empty));

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

        ShowDetails = model.ShowDetails;
        UpdateProperty(nameof(ShowDetails));

        _modelPlaceholderText = model.PlaceholderText;
        UpdateProperty(nameof(PlaceholderText));

        InitialSearchText = SearchText = model.SearchText;
        UpdateProperty(nameof(SearchText));
        UpdateProperty(nameof(InitialSearchText));

        EmptyContent = new(new(model.EmptyContent), PageContext);
        EmptyContent.SlowInitializeProperties();

        Filters = new(new(model.Filters), PageContext);
        Filters.InitializeProperties();
        UpdateProperty(nameof(Filters));

        FetchItems();
        model.ItemsChanged += Model_ItemsChanged;
    }

    private IGridPropertiesViewModel? LoadGridPropertiesViewModel(IGridProperties? gridProperties)
    {
        if (gridProperties is IMediumGridLayout mediumGridLayout)
        {
            return new MediumGridPropertiesViewModel(mediumGridLayout);
        }
        else if (gridProperties is IGalleryGridLayout galleryGridLayout)
        {
            return new GalleryGridPropertiesViewModel(galleryGridLayout);
        }
        else if (gridProperties is ISmallGridLayout smallGridLayout)
        {
            return new SmallGridPropertiesViewModel(smallGridLayout);
        }

        return null;
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
                Filters = new(new(model.Filters), PageContext);
                Filters.InitializeProperties();
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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _fetchItemsCancellationTokenSource?.Cancel();
        _fetchItemsCancellationTokenSource?.Dispose();
        _fetchItemsCancellationTokenSource = null;
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        EmptyContent?.SafeCleanup();
        EmptyContent = new(new(null), PageContext); // necessary?

        _cancellationTokenSource?.Cancel();
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

        Filters?.SafeCleanup();

        var model = _model.Unsafe;
        if (model is not null)
        {
            model.ItemsChanged -= Model_ItemsChanged;
        }
    }
}
