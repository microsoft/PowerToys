// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListViewModel : PageViewModel
{
    private readonly HashSet<ListItemViewModel> _itemCache = [];

    // TODO: Do we want a base "ItemsPageViewModel" for anything that's going to have items?

    // Observable from MVVM Toolkit will auto create public properties that use INotifyPropertyChange change
    // https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observablegroupedcollections for grouping support
    [ObservableProperty]
    public partial ObservableCollection<ListItemViewModel> Items { get; set; } = [];

    private readonly ExtensionObject<IListPage> _model;

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public bool ShowDetails { get; private set; }

    public string PlaceholderText { get => string.IsNullOrEmpty(field) ? "Type here to search..." : field; private set; } = string.Empty;

    public ListViewModel(IListPage model, TaskScheduler scheduler)
        : base(model, scheduler)
    {
        _model = new(model);
    }

    protected override void OnFilterUpdated(string filter)
    {
        //// TODO: Just temp testing, need to think about where we want to filter, as ACVS in View could be done, but then grouping need CVS, maybe we do grouping in view
        //// and manage filtering below, but we should be smarter about this and understand caching and other requirements...
        //// Investigate if we re-use src\modules\cmdpal\extensionsdk\Microsoft.CmdPal.Extensions.Helpers\ListHelpers.cs InPlaceUpdateList and FilterList?

        // Remove all items out right if we clear the filter, otherwise, recheck the items already displayed.
        if (string.IsNullOrWhiteSpace(filter))
        {
            Items.Clear();
        }
        else
        {
            // Remove any existing items which don't match the filter
            for (var i = Items.Count - 1; i >= 0; i--)
            {
                if (!Items[i].MatchesFilter(filter))
                {
                    Items.RemoveAt(i);
                }
            }
        }

        // Add back any new items which do match the filter
        foreach (var item in _itemCache)
        {
            if ((filter == string.Empty || item.MatchesFilter(filter))
                && !Items.Contains(item)) //// TODO: We should be smarter here somehow
            {
                Items.Add(item);
            }
        }
    }

    private void Model_ItemsChanged(object sender, ItemsChangedEventArgs args) => FetchItems();

    //// Run on background thread, from InitializeAsync or Model_ItemsChanged
    private void FetchItems()
    {
        // TEMPORARY: just plop all the items into a single group
        // see 9806fe5d8 for the last commit that had this with sections
        // TODO unsafe
        try
        {
            var newItems = _model.Unsafe!.GetItems();

            foreach (var item in newItems)
            {
                // TODO: When we fetch next page of items or refreshed items, we may need to check if we have an existing ViewModel in the cache?
                ListItemViewModel viewModel = new(item, this);
                viewModel.InitializeProperties();
                _itemCache.Add(viewModel); // TODO: Figure out when we clear/remove things from cache...

                // We may already have items from the new items here.
                if ((Filter == string.Empty || viewModel.MatchesFilter(Filter))
                    && !Items.Contains(viewModel)) //// TODO: We should be smarter about the contains here somehow (also in OnFilterUpdated)
                {
                    // Am I really allowed to modify that observable collection on a BG
                    // thread and have it just work in the UI??
                    Items.Add(viewModel);
                }
            }
        }
        catch (Exception ex)
        {
            ShowException(ex);
            throw;
        }
    }

    // InvokeItemCommand is what this will be in Xaml due to source generator
    [RelayCommand]
    private void InvokeItem(ListItemViewModel item) => WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item.Command));

    [RelayCommand]
    private void UpdateSelectedItem(ListItemViewModel item)
    {
        WeakReferenceMessenger.Default.Send<UpdateActionBarMessage>(new(item));

        if (ShowDetails && item.HasDetails)
        {
            WeakReferenceMessenger.Default.Send<ShowDetailsMessage>(new(item.Details));
        }
        else
        {
            WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
        }
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var listPage = _model.Unsafe;
        if (listPage == null)
        {
            return; // throw?
        }

        ShowDetails = listPage.ShowDetails;
        UpdateProperty(nameof(ShowDetails));
        PlaceholderText = listPage.PlaceholderText;
        UpdateProperty(nameof(PlaceholderText));

        FetchItems();
        listPage.ItemsChanged += Model_ItemsChanged;
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
                this.PlaceholderText = model.PlaceholderText;
                break;
        }

        UpdateProperty(propertyName);
    }
}
