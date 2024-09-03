// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using DeveloperCommandPalette;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;

namespace WindowsCommandPalette.Views;

public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return string.IsNullOrWhiteSpace((string)value) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class SectionInfoList : ObservableCollection<ListItemViewModel>
{
    public string Title { get; }

    private readonly DispatcherQueue DispatcherQueue = DispatcherQueue.GetForCurrentThread();

    public SectionInfoList(ISection? section, IEnumerable<ListItemViewModel> items) : base(items)
    {
        Title = section?.Title ?? string.Empty;
        if (section != null && section is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged -= Items_CollectionChanged;
            observable.CollectionChanged += Items_CollectionChanged;
        }
        if (this.DispatcherQueue == null)
        {
            throw new InvalidOperationException("DispatcherQueue is null");
        }
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        //DispatcherQueue.TryEnqueue(() => {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (var i in e.NewItems)
            {
                if (i is IListItem li)
                {
                    if (!string.IsNullOrEmpty(li.Title))
                    {
                        ListItemViewModel vm = new(li);
                        this.Add(vm);

                    }
                    //if (isDynamic)
                    //{
                    //    // Dynamic lists are in charge of their own
                    //    // filtering. They know if this thing was already
                    //    // filtered or not.
                    //    FilteredItems.Add(vm);
                    //}
                }
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            this.Clear();
            //Items.Clear();
            //if (isDynamic)
            //{
            //    FilteredItems.Clear();
            //}
        }
        //});
    }
}

public sealed class NoOpAction : InvokableCommand
{
    public override ICommandResult Invoke() { return ActionResult.KeepOpen(); }
}

public sealed class ErrorListItem : Microsoft.Windows.CommandPalette.Extensions.Helpers.ListItem
{
    public ErrorListItem(Exception ex) : base(new NoOpAction()) {
        this.Title = "Error in extension:";
        this.Subtitle = ex.Message;
    }
}

public sealed class ListPageViewModel : PageViewModel
{
    internal readonly ObservableCollection<SectionInfoList> Items = [];
    internal readonly ObservableCollection<SectionInfoList> FilteredItems = [];

    internal IListPage Page => (IListPage)this.pageAction;

    private bool isDynamic => Page is IDynamicListPage;

    private IDynamicListPage? dynamicPage => Page as IDynamicListPage;

    private readonly DispatcherQueue DispatcherQueue = DispatcherQueue.GetForCurrentThread();
    internal string Query = string.Empty;

    public ListPageViewModel(IListPage page) : base(page)
    {
    }

    internal Task InitialRender()
    {
        return UpdateListItems();
    }

    internal async Task UpdateListItems()
    {
        // on main thread

        var t = new Task<ISection[]>(() => {
            try
            {
                return dynamicPage != null ?
                    dynamicPage.GetItems(Query) :
                    this.Page.GetItems();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return [new ListSection() { Title = "Error", Items = [new ErrorListItem(ex)] }];
            }

        });
        t.Start();
        var sections = await t;

        // still on main thread

        // TODO! For dynamic lists, we're clearing out the whole list of items
        // we already have, then rebuilding it. We shouldn't do that. We should
        // still use the results from GetItems and put them into the code in
        // UpdateFilter to intelligently add/remove as needed.
        //Items.Clear();
        //FilteredItems.Clear();

        Collection<SectionInfoList> newItems = new();

        var size = sections.Length;
        for (var sectionIndex = 0; sectionIndex < size; sectionIndex++)
        {
            var section = sections[sectionIndex];
            var sectionItems = new SectionInfoList(
                section,
                section.Items
                    .Where(i => i != null && !string.IsNullOrEmpty(i.Title))
                    .Select(i => new ListItemViewModel(i))
                );

            // var items = section.Items;
            // for (var i = 0; i < items.Length; i++) {
            //     ListItemViewModel vm = new(items[i]);
            //     Items.Add(vm);
            //     FilteredItems.Add(vm);
            // }
            newItems.Add(sectionItems);

            // Items.Add(sectionItems);
            // FilteredItems.Add(sectionItems);
        }

        ListHelpers.InPlaceUpdateList(Items, newItems);
        ListHelpers.InPlaceUpdateList(FilteredItems, newItems);
    }

    internal async Task<Collection<SectionInfoList>> GetFilteredItems(string query) {

        if (query == Query)
        {
            return FilteredItems;
        }
        Query = query;
        if (isDynamic)
        {
            await UpdateListItems();
            return FilteredItems;
        }
        else
        {
            // Static lists don't need to re-fetch the items
            if (string.IsNullOrEmpty(query))
            {
                return Items;
            }

            //// TODO! Probably bad that this turns list view models into listitems back to NEW view models
            //return ListHelpers.FilterList(Items.Select(vm => vm.ListItem), Query).Select(li => new ListItemViewModel(li)).ToList();
            try{
                var allFilteredItems = ListHelpers.FilterList(
                    Items
                        .SelectMany(section => section)
                        .Select(vm => vm.ListItem.Unsafe),
                    Query).Select(li => new ListItemViewModel(li)
                );
                var newSection = new SectionInfoList(null, allFilteredItems);
                return [newSection];
            }
            catch (COMException ex)
            {
                return [new SectionInfoList(null, [new ListItemViewModel(new ErrorListItem(ex))])];
            }
        }
    }
}

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ListPage : Page, System.ComponentModel.INotifyPropertyChanged
{
    private ListPageViewModel? ViewModel;

    public event PropertyChangedEventHandler? PropertyChanged;

    private ListItemViewModel? _SelectedItem;

    public ListItemViewModel? SelectedItem
    {
        get => _SelectedItem;
        set
        {
            _SelectedItem = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(SelectedItem)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(MoreCommandsAvailable)));

            if (ViewModel != null && _SelectedItem?.Details != null && ViewModel.Page.ShowDetails)
            {
                this.DetailsContent.Child = new DetailsControl(_SelectedItem.Details);
                this.DetailsContent.Visibility = Visibility.Visible;
                this.DetailsColumn.Width = new GridLength(2, GridUnitType.Star);
            }
            else
            {
                this.DetailsContent.Child = null;
                this.DetailsContent.Visibility = Visibility.Collapsed;
                this.DetailsColumn.Width = GridLength.Auto;
            }
        }
    }

    private bool MoreCommandsAvailable
    {
        get
        {
            if (ItemsList.SelectedItem is not ListItemViewModel li) return false;
            return li.HasMoreCommands;
        }
    }

    public ListPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = (ListPageViewModel?)e.Parameter;
        if (ViewModel == null) return;

        if (e.NavigationMode == NavigationMode.New) {
            ViewModel.InitialRender().ContinueWith( (t) => {
                DispatcherQueue.TryEnqueue(async () => { await UpdateFilter(FilterBox.Text); });
            });

        }
        else
        {
            DispatcherQueue.TryEnqueue(async () => { await UpdateFilter(FilterBox.Text); });
        }

        this.ItemsCVS.Source = ViewModel?.FilteredItems;
        this.ItemsList.SelectedIndex = 0;
    }

    private void DoAction(ActionViewModel actionViewModel)
    {
        ViewModel?.DoAction(actionViewModel);
    }

    private void ListItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not ListViewItem listItem) return;
        if (listItem.DataContext is not ListItemViewModel li) return;
        if (e.OriginalKey == Windows.System.VirtualKey.Enter)
        {
            if (li.DefaultAction != null)
            {
                DoAction(new(li.DefaultAction));
            }
        }
    }

    private void MoreCommandsButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        FlyoutShowOptions options = new FlyoutShowOptions
        {
            ShowMode = FlyoutShowMode.Standard
        };
        MoreCommandsButton.Flyout.ShowAt(MoreCommandsButton, options);
        ActionsDropdown.SelectedIndex = 0;
        ActionsDropdown.Focus(FocusState.Programmatic);
    }

    private void ListItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not ListViewItem listItem) return;
        if (listItem.DataContext is not ListItemViewModel li) return;
        _ = li;
        // For a bit I had double-clicks Invoke and single just select, but that crashes?
        //ItemsList.SelectedItem = listItem;

        if (li.DefaultAction != null)
        {
            DoAction(new(li.DefaultAction));
        }
    }

    private void ListViewItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        //if (sender is not ListViewItem listItem) return;
        //if (listItem.DataContext is not ListItemViewModel li) return;
        //if (li.DefaultAction != null)
        //{
        //    DoAction(new(li.DefaultAction));
        //}
    }

    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView lv) return;
        if (lv.SelectedItem is not ListItemViewModel li) return;
        SelectedItem = li;
    }

    private void ActionListViewItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not ListViewItem listItem) return;
        if (listItem.DataContext is not ContextItemViewModel vm) return;
        if (e.Key == Windows.System.VirtualKey.Enter || e.Key == Windows.System.VirtualKey.Space)
        {
            DoAction(new(vm.Command));
            e.Handled = true;
        }
    }

    private void ActionListViewItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not ListViewItem listItem) return;
        if (listItem.DataContext is not ContextItemViewModel vm) return;
        DoAction(new(vm.Command));
        e.Handled = true;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        FilterBox.Focus(FocusState.Programmatic);
        if (ItemsList.Items.Count > 0)
        {
            ItemsList.SelectedIndex = 0;
        }
    }

    private void FilterBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled) return;

        var ctrlPressed = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (e.Key == Windows.System.VirtualKey.Up || e.Key == Windows.System.VirtualKey.Down)
        {
            var newIndex = e.Key == Windows.System.VirtualKey.Up ? ItemsList.SelectedIndex - 1 : ItemsList.SelectedIndex + 1;
            if (newIndex >= 0 && newIndex < ItemsList.Items.Count)
            {
                ItemsList.SelectedIndex = newIndex;
                ItemsList.ScrollIntoView(ItemsList.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Enter /* && ItemsList.SelectedItem != null */)
        {
            if (ItemsList.SelectedItem is ListItemViewModel li)
            {
                if (li.DefaultAction != null)
                {
                    DoAction(new(li.DefaultAction));
                }
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (FilterBox.Text.Length > 0)
            {
                FilterBox.Text = string.Empty;
            }
            else
            {
                ViewModel?.GoBack();
            }
            e.Handled = true;
        }
        // ctrl+k
        else if (ctrlPressed && e.Key == Windows.System.VirtualKey.K)
        {
            // Open the more actions flyout and focus the first item
            if (ActionsDropdown.Items.Count > 0)
            {
                FlyoutShowOptions options = new FlyoutShowOptions
                {
                    ShowMode = FlyoutShowMode.Standard
                };
                MoreCommandsButton.Flyout.ShowAt(MoreCommandsButton, options);
                ActionsDropdown.SelectedIndex = 0;
                ActionsDropdown.Focus(FocusState.Programmatic);
            }
        }
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel == null) return;
        // on the UI thread
        _ = UpdateFilter(FilterBox.Text);
    }

    private async Task UpdateFilter(string text)
    {
        if (ViewModel == null) return;

        // ViewModel.Query = text;

        // This first part will first filter all the commands that were passed
        // into us initially. We handle the filtering of these ones. Commands
        // from async querying happens later.
        var newMatches = await ViewModel.GetFilteredItems(text);
        // this.ItemsCVS.Source = ViewModel.FilteredItems;
        // Returns back on the UI thread
        ListHelpers.InPlaceUpdateList(ViewModel.FilteredItems, newMatches);
        // for (var i = 0; i < ViewModel.FilteredItems.Count && i < newMatches.Count; i++)
        // {
        //     for (var j = i; j < ViewModel.FilteredItems.Count; j++)
        //     {
        //         if (ViewModel.FilteredItems[j] == newMatches[i])
        //         {
        //             for (var k = i; k < j; k++)
        //             {
        //                 ViewModel.FilteredItems.RemoveAt(i);
        //             }
        //             break;
        //         }
        //     }

        //     if (ViewModel.FilteredItems[i] != newMatches[i])
        //     {
        //         ViewModel.FilteredItems.Insert(i, newMatches[i]);
        //     }
        // }

        // // Remove any extra trailing items from the destination
        // while (ViewModel.FilteredItems.Count > newMatches.Count)
        // {
        //     ViewModel.FilteredItems.RemoveAt(ViewModel.FilteredItems.Count - 1);//RemoveAtEnd
        // }

        // // Add any extra trailing items from the source
        // while (ViewModel.FilteredItems.Count < newMatches.Count)
        // {
        //     ViewModel.FilteredItems.Add(newMatches[ViewModel.FilteredItems.Count]);
        // }

        // set the selected index to the first item in the list
        if (ItemsList.Items.Count > 0)
        {
            ItemsList.SelectedIndex = 0;
            ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }
    }

    private void BackButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel?.GoBack();
    }
}
