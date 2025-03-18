// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

public sealed partial class ListPage : Page,
    IRecipient<NavigateNextCommand>,
    IRecipient<NavigatePreviousCommand>,
    IRecipient<ActivateSelectedListItemMessage>,
    IRecipient<ActivateSecondaryCommandMessage>
{
    private ListViewModel? ViewModel
    {
        get => (ListViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ListViewModel), typeof(ListPage), new PropertyMetadata(null, OnViewModelChanged));

    public ListPage()
    {
        this.InitializeComponent();
        this.NavigationCacheMode = NavigationCacheMode.Disabled;
        this.ItemsList.Loaded += ItemsList_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is ListViewModel lvm)
        {
            ViewModel = lvm;
        }

        if (e.NavigationMode == NavigationMode.Back
            || (e.NavigationMode == NavigationMode.New && ItemsList.Items.Count > 0))
        {
            // Upon navigating _back_ to this page, immediately select the
            // first item in the list
            ItemsList.SelectedIndex = 0;
        }

        // RegisterAll isn't AOT compatible
        WeakReferenceMessenger.Default.Register<NavigateNextCommand>(this);
        WeakReferenceMessenger.Default.Register<NavigatePreviousCommand>(this);
        WeakReferenceMessenger.Default.Register<ActivateSelectedListItemMessage>(this);
        WeakReferenceMessenger.Default.Register<ActivateSecondaryCommandMessage>(this);

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        WeakReferenceMessenger.Default.Unregister<NavigateNextCommand>(this);
        WeakReferenceMessenger.Default.Unregister<NavigatePreviousCommand>(this);
        WeakReferenceMessenger.Default.Unregister<ActivateSelectedListItemMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ActivateSecondaryCommandMessage>(this);

        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.ItemsUpdated -= Page_ItemsUpdated;
        }

        if (e.NavigationMode != NavigationMode.New)
        {
            ViewModel?.SafeCleanup();
            CleanupHelper.Cleanup(this);
            Bindings.StopTracking();
        }

        // Clean-up event listeners
        ViewModel = null;

        GC.Collect();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS is too aggressive at pruning methods bound in XAML")]
    private void ItemsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ListItemViewModel item)
        {
            var settings = App.Current.Services.GetService<SettingsModel>()!;
            if (settings.SingleClickActivates)
            {
                ViewModel?.InvokeItemCommand.Execute(item);
            }
            else
            {
                ViewModel?.UpdateSelectedItemCommand.Execute(item);
                WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
            }
        }
    }

    private void ItemsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ItemsList.SelectedItem is ListItemViewModel vm)
        {
            var settings = App.Current.Services.GetService<SettingsModel>()!;
            if (!settings.SingleClickActivates)
            {
                ViewModel?.InvokeItemCommand.Execute(vm);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS is too aggressive at pruning methods bound in XAML")]
    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsList.SelectedItem is ListItemViewModel item)
        {
            var vm = ViewModel;
            _ = Task.Run(() =>
            {
                vm?.UpdateSelectedItemCommand.Execute(item);
            });
        }

        // There's mysterious behavior here, where the selection seemingly
        // changes to _nothing_ when we're backspacing to a single character.
        // And at that point, seemingly the item that's getting removed is not
        // a member of FilteredItems. Very bizarre.
        //
        // Might be able to fix in the future by stashing the removed item
        // here, then in Page_ItemsUpdated trying to select that cached item if
        // it's in the list (otherwise, clear the cache), but that seems
        // aggressively BODGY for something that mostly just works today.
        if (ItemsList.SelectedItem != null)
        {
            ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }
    }

    private void ItemsList_Loaded(object sender, RoutedEventArgs e)
    {
        // Find the ScrollViewer in the ListView
        var listViewScrollViewer = FindScrollViewer(this.ItemsList);

        if (listViewScrollViewer != null)
        {
            listViewScrollViewer.ViewChanged += ListViewScrollViewer_ViewChanged;
        }
    }

    private void ListViewScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        var scrollView = sender as ScrollViewer;
        if (scrollView == null)
        {
            return;
        }

        // When we get to the bottom, request more from the extension, if they
        // have more to give us.
        // We're checking when we get to 80% of the scroll height, to give the
        // extension a bit of a heads-up before the user actually gets there.
        if (scrollView.VerticalOffset >= (scrollView.ScrollableHeight * .8))
        {
            ViewModel?.LoadMoreIfNeeded();
        }
    }

    public void Receive(NavigateNextCommand message)
    {
        // Note: We may want to just have the notion of a 'SelectedCommand' in our VM
        // And then have these commands manipulate that state being bound to the UI instead
        // We may want to see how other non-list UIs need to behave to make this decision
        // At least it's decoupled from the SearchBox now :)
        if (ItemsList.SelectedIndex < ItemsList.Items.Count - 1)
        {
            ItemsList.SelectedIndex++;
        }
    }

    public void Receive(NavigatePreviousCommand message)
    {
        if (ItemsList.SelectedIndex > 0)
        {
            ItemsList.SelectedIndex--;
        }
    }

    public void Receive(ActivateSelectedListItemMessage message)
    {
        if (ViewModel?.ShowEmptyContent ?? false)
        {
            ViewModel?.InvokeItemCommand.Execute(null);
        }
        else if (ItemsList.SelectedItem is ListItemViewModel item)
        {
            ViewModel?.InvokeItemCommand.Execute(item);
        }
    }

    public void Receive(ActivateSecondaryCommandMessage message)
    {
        if (ViewModel?.ShowEmptyContent ?? false)
        {
            ViewModel?.InvokeSecondaryCommandCommand.Execute(null);
        }
        else if (ItemsList.SelectedItem is ListItemViewModel item)
        {
            ViewModel?.InvokeSecondaryCommandCommand.Execute(item);
        }
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListPage @this)
        {
            if (e.OldValue is ListViewModel old)
            {
                old.PropertyChanged -= @this.ViewModel_PropertyChanged;
                old.ItemsUpdated -= @this.Page_ItemsUpdated;
            }

            if (e.NewValue is ListViewModel page)
            {
                page.PropertyChanged += @this.ViewModel_PropertyChanged;
                page.ItemsUpdated += @this.Page_ItemsUpdated;
            }
            else if (e.NewValue == null)
            {
                Logger.LogDebug("cleared viewmodel");
            }
        }
    }

    // Called after we've finished updating the whole list for either a
    // GetItems or a change in the filter.
    private void Page_ItemsUpdated(ListViewModel sender, object args)
    {
        // If for some reason, we don't have a selected item, fix that.
        //
        // It's important to do this here, because once there's no selection
        // (which can happen as the list updates) we won't get an
        // ItemsList_SelectionChanged again to give us another chance to change
        // the selection from null -> something. Better to just update the
        // selection once, at the end of all the updating.
        if (ItemsList.SelectedItem == null)
        {
            ItemsList.SelectedIndex = 0;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var prop = e.PropertyName;
        if (prop == nameof(ViewModel.FilteredItems))
        {
            Debug.WriteLine($"ViewModel.FilteredItems {ItemsList.SelectedItem}");
        }
    }

    private ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        if (parent is ScrollViewer)
        {
            return (ScrollViewer)parent;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            var result = FindScrollViewer(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
