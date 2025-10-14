// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Commands;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace Microsoft.CmdPal.UI;

public sealed partial class ListPage : Page,
    IRecipient<NavigateNextCommand>,
    IRecipient<NavigatePreviousCommand>,
    IRecipient<ActivateSelectedListItemMessage>,
    IRecipient<ActivateSecondaryCommandMessage>
{
    private InputSource _lastInputSource;

    internal ListViewModel? ViewModel
    {
        get => (ListViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ListViewModel), typeof(ListPage), new PropertyMetadata(null, OnViewModelChanged));

    private ListViewBase ItemView
    {
        get
        {
            return ViewModel?.IsGridView == true ? ItemsGrid : ItemsList;
        }
    }

    public ListPage()
    {
        this.InitializeComponent();
        this.NavigationCacheMode = NavigationCacheMode.Disabled;
        this.ItemView.Loaded += Items_Loaded;
        this.ItemView.PreviewKeyDown += Items_PreviewKeyDown;
        this.ItemView.PointerPressed += Items_PointerPressed;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not AsyncNavigationRequest navigationRequest)
        {
            throw new InvalidOperationException($"Invalid navigation parameter: {nameof(e.Parameter)} must be {nameof(AsyncNavigationRequest)}");
        }

        if (navigationRequest.TargetViewModel is not ListViewModel listViewModel)
        {
            throw new InvalidOperationException($"Invalid navigation target: AsyncNavigationRequest.{nameof(AsyncNavigationRequest.TargetViewModel)} must be {nameof(ListViewModel)}");
        }

        ViewModel = listViewModel;

        if (e.NavigationMode == NavigationMode.Back
            || (e.NavigationMode == NavigationMode.New && ItemView.Items.Count > 0))
        {
            // Upon navigating _back_ to this page, immediately select the
            // first item in the list
            ItemView.SelectedIndex = 0;
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

        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.ItemsUpdated -= Page_ItemsUpdated;
        }

        if (e.NavigationMode != NavigationMode.New)
        {
            ViewModel?.SafeCleanup();
            CleanupHelper.Cleanup(this);
        }

        // Clean-up event listeners
        ViewModel = null;

        GC.Collect();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS is too aggressive at pruning methods bound in XAML")]
    private void Items_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ListItemViewModel item)
        {
            if (_lastInputSource == InputSource.Keyboard)
            {
                ViewModel?.InvokeItemCommand.Execute(item);
                return;
            }

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

    private void Items_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ItemView.SelectedItem is ListItemViewModel vm)
        {
            var settings = App.Current.Services.GetService<SettingsModel>()!;
            if (!settings.SingleClickActivates)
            {
                ViewModel?.InvokeItemCommand.Execute(vm);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS is too aggressive at pruning methods bound in XAML")]
    private void Items_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var vm = ViewModel;
        var li = ItemView.SelectedItem as ListItemViewModel;
        _ = Task.Run(() =>
        {
            vm?.UpdateSelectedItemCommand.Execute(li);
        });

        // There's mysterious behavior here, where the selection seemingly
        // changes to _nothing_ when we're backspacing to a single character.
        // And at that point, seemingly the item that's getting removed is not
        // a member of FilteredItems. Very bizarre.
        //
        // Might be able to fix in the future by stashing the removed item
        // here, then in Page_ItemsUpdated trying to select that cached item if
        // it's in the list (otherwise, clear the cache), but that seems
        // aggressively BODGY for something that mostly just works today.
        if (ItemView.SelectedItem is not null)
        {
            ItemView.ScrollIntoView(ItemView.SelectedItem);

            // Automation notification for screen readers
            var listViewPeer = Microsoft.UI.Xaml.Automation.Peers.ListViewAutomationPeer.CreatePeerForElement(ItemView);
            if (listViewPeer is not null && li is not null)
            {
                var notificationText = li.Title;

                UIHelper.AnnounceActionForAccessibility(
                     ItemsList,
                     notificationText,
                     "CommandPaletteSelectedItemChanged");
            }
        }
    }

    private void Items_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement element &&
            element.DataContext is ListItemViewModel item)
        {
            if (ItemView.SelectedItem != item)
            {
                ItemView.SelectedItem = item;
            }

            ViewModel?.UpdateSelectedItemCommand.Execute(item);

            var pos = e.GetPosition(element);

            _ = DispatcherQueue.TryEnqueue(
                () =>
                {
                    WeakReferenceMessenger.Default.Send<OpenContextMenuMessage>(
                        new OpenContextMenuMessage(
                            element,
                            Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft,
                            pos,
                            ContextMenuFilterLocation.Top));
                });
        }
    }

    private void Items_Loaded(object sender, RoutedEventArgs e)
    {
        // Find the ScrollViewer in the ItemView (ItemsList or ItemsGrid)
        var listViewScrollViewer = FindScrollViewer(this.ItemView);

        if (listViewScrollViewer is not null)
        {
            listViewScrollViewer.ViewChanged += ListViewScrollViewer_ViewChanged;
        }
    }

    private void ListViewScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        var scrollView = sender as ScrollViewer;
        if (scrollView is null)
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
        if (ItemView.SelectedIndex < ItemView.Items.Count - 1)
        {
            ItemView.SelectedIndex++;
        }
        else
        {
            ItemView.SelectedIndex = 0;
        }
    }

    public void Receive(NavigatePreviousCommand message)
    {
        if (ItemView.SelectedIndex > 0)
        {
            ItemView.SelectedIndex--;
        }
        else
        {
            ItemView.SelectedIndex = ItemView.Items.Count - 1;
        }
    }

    public void Receive(ActivateSelectedListItemMessage message)
    {
        if (ViewModel?.ShowEmptyContent ?? false)
        {
            ViewModel?.InvokeItemCommand.Execute(null);
        }
        else if (ItemView.SelectedItem is ListItemViewModel item)
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
        else if (ItemView.SelectedItem is ListItemViewModel item)
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
            else if (e.NewValue is null)
            {
                Logger.LogDebug("cleared view model");
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
        // ItemView_SelectionChanged again to give us another chance to change
        // the selection from null -> something. Better to just update the
        // selection once, at the end of all the updating.
        if (ItemView.SelectedItem is null)
        {
            ItemView.SelectedIndex = 0;
        }

        // Always reset the selected item when the top-level list page changes
        // its items
        if (!sender.IsNested)
        {
            ItemView.SelectedIndex = 0;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var prop = e.PropertyName;
        if (prop == nameof(ViewModel.FilteredItems))
        {
            Debug.WriteLine($"ViewModel.FilteredItems {ItemView.SelectedItem}");
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
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private void Items_OnContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        var (item, element) = e.OriginalSource switch
        {
            // caused by keyboard shortcut (e.g. Context menu key or Shift+F10)
            SelectorItem selectorItem => (ItemView.ItemFromContainer(selectorItem) as ListItemViewModel, selectorItem),

            // caused by right-click on the ListViewItem
            FrameworkElement { DataContext: ListItemViewModel itemViewModel } frameworkElement => (itemViewModel, frameworkElement),

            _ => (null, null),
        };

        if (item is null || element is null)
        {
            return;
        }

        if (ItemView.SelectedItem != item)
        {
            ItemView.SelectedItem = item;
        }

        if (!e.TryGetPosition(element, out var pos))
        {
            pos = new(0, element.ActualHeight);
        }

        _ = DispatcherQueue.TryEnqueue(
            () =>
            {
                WeakReferenceMessenger.Default.Send<OpenContextMenuMessage>(
                    new OpenContextMenuMessage(
                        element,
                        Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft,
                        pos,
                        ContextMenuFilterLocation.Top));
            });
        e.Handled = true;
    }

    private void Items_OnContextCanceled(UIElement sender, RoutedEventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(() => WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>());
    }

    private void Items_PointerPressed(object sender, PointerRoutedEventArgs e) => _lastInputSource = InputSource.Pointer;

    private void Items_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter or VirtualKey.Space)
        {
            _lastInputSource = InputSource.Keyboard;
        }
    }

    private enum InputSource
    {
        None,
        Keyboard,
        Pointer,
    }
}
