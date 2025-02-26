// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListViewModel : PageViewModel
{
    // private readonly HashSet<ListItemViewModel> _itemCache = [];

    // TODO: Do we want a base "ItemsPageViewModel" for anything that's going to have items?

    // Observable from MVVM Toolkit will auto create public properties that use INotifyPropertyChange change
    // https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observablegroupedcollections for grouping support
    [ObservableProperty]
    public partial ObservableCollection<ListItemViewModel> FilteredItems { get; set; } = [];

    public ObservableCollection<ListItemViewModel> Items { get; set; } = [];

    private readonly ExtensionObject<IListPage> _model;

    private readonly Lock _listLock = new();

    public event TypedEventHandler<ListViewModel, object>? ItemsUpdated;

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public bool ShowDetails { get; private set; }

    public string ModelPlaceholderText { get => string.IsNullOrEmpty(field) ? "Type here to search..." : field; private set; } = string.Empty;

    public override string PlaceholderText => ModelPlaceholderText;

    public string SearchText { get; private set; } = string.Empty;

    private bool _isDynamic;

    public ListViewModel(IListPage model, TaskScheduler scheduler, CommandPaletteHost host)
        : base(model, scheduler, host)
    {
        _model = new(model);
    }

    // TODO: Does this need to hop to a _different_ thread, so that we don't block the extension while we're fetching?
    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args) => FetchItems();

    protected override void OnFilterUpdated(string filter)
    {
        //// TODO: Just temp testing, need to think about where we want to filter, as ACVS in View could be done, but then grouping need CVS, maybe we do grouping in view
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
                        dynamic.SearchText = filter;
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
        }
    }

    //// Run on background thread, from InitializeAsync or Model_ItemsChanged
    private void FetchItems()
    {
        // TEMPORARY: just plop all the items into a single group
        // see 9806fe5d8 for the last commit that had this with sections
        try
        {
            var newItems = _model.Unsafe!.GetItems();

            // Collect all the items into new viewmodels
            Collection<ListItemViewModel> newViewModels = [];

            // TODO we can probably further optimize this by also keeping a
            // HashSet of every ExtensionObject we currently have, and only
            // building new viewmodels for the ones we haven't already built.
            foreach (var item in newItems)
            {
                ListItemViewModel viewModel = new(item, this);

                // If an item fails to load, silently ignore it.
                if (viewModel.SafeInitializeProperties())
                {
                    newViewModels.Add(viewModel);
                }
            }

            lock (_listLock)
            {
                // Now that we have new ViewModels for everything from the
                // extension, smartly update our list of VMs
                ListHelpers.InPlaceUpdateList(Items, newViewModels);
            }

            // TODO: Iterate over everything in Items, and prune items from the
            // cache if we don't need them anymore
        }
        catch (Exception ex)
        {
            // TODO: Move this within the for loop, so we can catch issues with individual items
            // Create a special ListItemViewModel for errors and use an ItemTemplateSelector in the ListPage to display error items differently.
            ShowException(ex, _model?.Unsafe?.Name);
            throw;
        }

        Task.Factory.StartNew(
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
                        ListHelpers.InPlaceUpdateList(FilteredItems, Items);
                    }
                }

                ItemsUpdated?.Invoke(this, EventArgs.Empty);
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            PageContext.Scheduler);
    }

    /// <summary>
    /// Apply our current filter text to the list of items, and update
    /// FilteredItems to match the results.
    /// </summary>
    private void ApplyFilterUnderLock() => ListHelpers.InPlaceUpdateList(FilteredItems, FilterList(Items, Filter));

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
            .Select(li => new ScoredListItemViewModel() { ViewModel = li, Score = ScoreListItem(query, li) })
            .Where(score => score.Score > 0)
            .OrderByDescending(score => score.Score);
        return scores
            .Select(score => score.ViewModel);
    }

    // InvokeItemCommand is what this will be in Xaml due to source generator
    [RelayCommand]
    private void InvokeItem(ListItemViewModel item) =>
        WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item.Command.Model, item.Model));

    [RelayCommand]
    private void InvokeSecondaryCommand(ListItemViewModel item)
    {
        if (item.SecondaryCommand != null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item.SecondaryCommand.Command.Model, item.Model));
        }
    }

    [RelayCommand]
    private void UpdateSelectedItem(ListItemViewModel item)
    {
        // GH #322:
        // For inexplicable reasons, if you try updating the command bar and
        // the details on the same UI thread tick as updating the list, we'll
        // explode
        Task.Factory.StartNew(
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
           },
           CancellationToken.None,
           TaskCreationOptions.None,
           PageContext.Scheduler);
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var listPage = _model.Unsafe;
        if (listPage == null)
        {
            return; // throw?
        }

        _isDynamic = listPage is IDynamicListPage;

        ShowDetails = listPage.ShowDetails;
        UpdateProperty(nameof(ShowDetails));

        ModelPlaceholderText = listPage.PlaceholderText;
        UpdateProperty(nameof(PlaceholderText));

        SearchText = listPage.SearchText;
        UpdateProperty(nameof(SearchText));

        FetchItems();
        listPage.ItemsChanged += Model_ItemsChanged;
    }

    public void LoadMoreIfNeeded()
    {
        if (_model.Unsafe?.HasMoreItems ?? false)
        {
            _model.Unsafe.LoadMore();
        }
    }

    protected override void FetchProperty(string propertyName)
    {
        base.FetchProperty(propertyName);

        var model = this._model.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(ShowDetails):
                this.ShowDetails = model.ShowDetails;
                break;
            case nameof(PlaceholderText):
                this.ModelPlaceholderText = model.PlaceholderText;
                break;
            case nameof(SearchText):
                this.SearchText = model.SearchText;
                break;
        }

        UpdateProperty(propertyName);
    }
}
