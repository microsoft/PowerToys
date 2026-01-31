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
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;

namespace Microsoft.CmdPal.UI;

public sealed partial class ListPage : Page,
    IRecipient<NavigateNextCommand>,
    IRecipient<NavigatePreviousCommand>,
    IRecipient<NavigateLeftCommand>,
    IRecipient<NavigateRightCommand>,
    IRecipient<NavigatePageDownCommand>,
    IRecipient<NavigatePageUpCommand>,
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

        if (e.NavigationMode == NavigationMode.Back)
        {
            // Must dispatch the selection to run at a lower priority; otherwise, GetFirstSelectableIndex
            // may return an incorrect index because item containers are not yet rendered.
            _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                var firstUsefulIndex = GetFirstSelectableIndex();
                if (firstUsefulIndex != -1)
                {
                    ItemView.SelectedIndex = firstUsefulIndex;
                }
            });
        }

        // RegisterAll isn't AOT compatible
        WeakReferenceMessenger.Default.Register<NavigateNextCommand>(this);
        WeakReferenceMessenger.Default.Register<NavigatePreviousCommand>(this);
        WeakReferenceMessenger.Default.Register<NavigateLeftCommand>(this);
        WeakReferenceMessenger.Default.Register<NavigateRightCommand>(this);
        WeakReferenceMessenger.Default.Register<NavigatePageDownCommand>(this);
        WeakReferenceMessenger.Default.Register<NavigatePageUpCommand>(this);
        WeakReferenceMessenger.Default.Register<ActivateSelectedListItemMessage>(this);
        WeakReferenceMessenger.Default.Register<ActivateSecondaryCommandMessage>(this);

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        WeakReferenceMessenger.Default.Unregister<NavigateNextCommand>(this);
        WeakReferenceMessenger.Default.Unregister<NavigatePreviousCommand>(this);
        WeakReferenceMessenger.Default.Unregister<NavigateLeftCommand>(this);
        WeakReferenceMessenger.Default.Unregister<NavigateRightCommand>(this);
        WeakReferenceMessenger.Default.Unregister<NavigatePageDownCommand>(this);
        WeakReferenceMessenger.Default.Unregister<NavigatePageUpCommand>(this);
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

    /// <summary>
    /// Finds the index of the first item in the list that is not a separator.
    /// Returns -1 if the list is empty or only contains separators.
    /// </summary>
    private int GetFirstSelectableIndex()
    {
        var items = ItemView.Items;
        if (items is null || items.Count == 0)
        {
            return -1;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (!IsSeparator(items[i]))
            {
                return i;
            }
        }

        return -1;
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
        if (ItemView.SelectedItem is not null && !IsSeparator(ItemView.SelectedItem))
        {
            var items = ItemView.Items;
            var firstUsefulIndex = GetFirstSelectableIndex();
            var shouldScroll = false;

            if (e.RemovedItems.Count > 0)
            {
                shouldScroll = true;
            }
            else if (ItemView.SelectedIndex > firstUsefulIndex)
            {
                shouldScroll = true;
            }

            if (shouldScroll)
            {
                ItemView.ScrollIntoView(ItemView.SelectedItem);
            }

            // Automation notification for screen readers
            var listViewPeer = Microsoft.UI.Xaml.Automation.Peers.ListViewAutomationPeer.CreatePeerForElement(ItemView);
            if (listViewPeer is not null && li is not null)
            {
                UIHelper.AnnounceActionForAccessibility(
                    ItemsList,
                    li.Title,
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
        if (ViewModel?.IsGridView == true)
        {
            // For grid views, use spatial navigation (down)
            HandleGridArrowNavigation(VirtualKey.Down);
        }
        else
        {
            // For list views, use simple linear navigation
            NavigateDown();
        }
    }

    public void Receive(NavigatePreviousCommand message)
    {
        if (ViewModel?.IsGridView == true)
        {
            // For grid views, use spatial navigation (up)
            HandleGridArrowNavigation(VirtualKey.Up);
        }
        else
        {
            NavigateUp();
        }
    }

    public void Receive(NavigateLeftCommand message)
    {
        // For grid views, use spatial navigation. For list views, just move up.
        if (ViewModel?.IsGridView == true)
        {
            HandleGridArrowNavigation(VirtualKey.Left);
        }
        else
        {
            // In list view, left arrow doesn't navigate
            // This maintains consistency with the SearchBar behavior
        }
    }

    public void Receive(NavigateRightCommand message)
    {
        // For grid views, use spatial navigation. For list views, just move down.
        if (ViewModel?.IsGridView == true)
        {
            HandleGridArrowNavigation(VirtualKey.Right);
        }
        else
        {
            // In list view, right arrow doesn't navigate
            // This maintains consistency with the SearchBar behavior
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

    public void Receive(NavigatePageDownCommand message)
    {
        var indexes = CalculateTargetIndexPageUpDownScrollTo(true);
        if (indexes is null)
        {
            return;
        }

        if (indexes.Value.CurrentIndex != indexes.Value.TargetIndex)
        {
            ItemView.SelectedIndex = indexes.Value.TargetIndex;
            if (ItemView.SelectedItem is not null)
            {
                ItemView.ScrollIntoView(ItemView.SelectedItem);
            }
        }
    }

    public void Receive(NavigatePageUpCommand message)
    {
        var indexes = CalculateTargetIndexPageUpDownScrollTo(false);
        if (indexes is null)
        {
            return;
        }

        if (indexes.Value.CurrentIndex != indexes.Value.TargetIndex)
        {
            ItemView.SelectedIndex = indexes.Value.TargetIndex;
            if (ItemView.SelectedItem is not null)
            {
                ItemView.ScrollIntoView(ItemView.SelectedItem);
            }
        }
    }

    /// <summary>
    /// Calculates the item index to target when performing a page up or page down
    /// navigation. The calculation attempts to estimate how many items fit into
    /// the visible viewport by measuring actual container heights currently visible
    /// within the internal ScrollViewer. If measurements are not available a
    /// fallback estimate is used.
    /// </summary>
    /// <param name="isPageDown">True to calculate a page-down target, false for page-up.</param>
    /// <returns>
    /// A tuple containing the current index and the calculated target index, or null
    /// if a valid calculation could not be performed (for example, missing ScrollViewer).
    /// </returns>
    private (int CurrentIndex, int TargetIndex)? CalculateTargetIndexPageUpDownScrollTo(bool isPageDown)
    {
        var scroll = FindScrollViewer(ItemView);
        if (scroll is null)
        {
            return null;
        }

        var viewportHeight = scroll.ViewportHeight;
        if (viewportHeight <= 0)
        {
            return null;
        }

        var currentIndex = ItemView.SelectedIndex < 0 ? 0 : ItemView.SelectedIndex;
        var itemCount = ItemView.Items.Count;

        // Compute visible item heights within the ScrollViewer viewport
        const int firstVisibleIndexNotFound = -1;
        var firstVisibleIndex = firstVisibleIndexNotFound;
        var visibleHeights = new List<double>(itemCount);

        for (var i = 0; i < itemCount; i++)
        {
            if (ItemView.ContainerFromIndex(i) is FrameworkElement container)
            {
                try
                {
                    var transform = container.TransformToVisual(scroll);
                    var topLeft = transform.TransformPoint(new Point(0, 0));
                    var bottom = topLeft.Y + container.ActualHeight;

                    // If any part of the container is inside the viewport, consider it visible
                    if (topLeft.Y >= 0 && bottom <= viewportHeight)
                    {
                        if (firstVisibleIndex == firstVisibleIndexNotFound)
                        {
                            firstVisibleIndex = i;
                        }

                        visibleHeights.Add(container.ActualHeight > 0 ? container.ActualHeight : 0);
                    }
                }
                catch
                {
                    // ignore transform errors and continue
                }
            }
        }

        var itemsPerPage = 0;

        // Calculate how many items fit in the viewport based on their actual heights
        if (visibleHeights.Count > 0)
        {
            double accumulated = 0;
            for (var i = 0; i < visibleHeights.Count; i++)
            {
                accumulated += visibleHeights[i] <= 0 ? 1 : visibleHeights[i];
                itemsPerPage++;
                if (accumulated >= viewportHeight)
                {
                    break;
                }
            }
        }
        else
        {
            // fallback: estimate using first measured container height
            double itemHeight = 0;
            for (var i = currentIndex; i < itemCount; i++)
            {
                if (ItemView.ContainerFromIndex(i) is FrameworkElement { ActualHeight: > 0 } c)
                {
                    itemHeight = c.ActualHeight;
                    break;
                }
            }

            if (itemHeight <= 0)
            {
                itemHeight = 1;
            }

            itemsPerPage = Math.Max(1, (int)Math.Floor(viewportHeight / itemHeight));
        }

        var targetIndex = isPageDown
                              ? Math.Min(itemCount - 1, currentIndex + Math.Max(1, itemsPerPage))
                              : Math.Max(0, currentIndex - Math.Max(1, itemsPerPage));

        return (currentIndex, targetIndex);
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
        // The selection logic must be deferred to the DispatcherQueue
        // to ensure the UI has processed the updated ItemsSource binding,
        // preventing ItemView.Items from appearing empty/null immediately after update.
        _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            var items = ItemView.Items;

            // If the list is null or empty, clears the selection and return
            if (items is null || items.Count == 0)
            {
                ItemView.SelectedIndex = -1;
                return;
            }

            // Finds the first item that is not a separator
            var firstUsefulIndex = GetFirstSelectableIndex();

            // If there is only separators in the list, don't select anything.
            if (firstUsefulIndex == -1)
            {
                ItemView.SelectedIndex = -1;

                return;
            }

            var shouldUpdateSelection = false;

            // If it's a top level list update we force the reset to the top useful item
            if (!sender.IsNested)
            {
                shouldUpdateSelection = true;
            }

            // No current selection or current selection is null
            else if (ItemView.SelectedItem is null)
            {
                shouldUpdateSelection = true;
            }

            // The current selected item is a separator
            else if (IsSeparator(ItemView.SelectedItem))
            {
                shouldUpdateSelection = true;
            }

            // The selected item does not exist in the new list
            else if (!items.Contains(ItemView.SelectedItem))
            {
                shouldUpdateSelection = true;
            }

            if (shouldUpdateSelection)
            {
                if (firstUsefulIndex != -1)
                {
                    ItemView.SelectedIndex = firstUsefulIndex;
                }
            }
        });
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var prop = e.PropertyName;
        if (prop == nameof(ViewModel.FilteredItems))
        {
            Debug.WriteLine($"ViewModel.FilteredItems {ItemView.SelectedItem}");
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        if (parent is ScrollViewer viewer)
        {
            return viewer;
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

    // Find a logical neighbor in the requested direction using containers' positions.
    private void HandleGridArrowNavigation(VirtualKey key)
    {
        if (ItemView.Items.Count == 0)
        {
            // No items, goodbye.
            return;
        }

        var currentIndex = ItemView.SelectedIndex;
        if (currentIndex < 0)
        {
            // -1 is a valid value (no item currently selected)
            currentIndex = 0;
            ItemView.SelectedIndex = 0;
        }

        try
        {
            // Try to compute using container positions; if not available, fall back to simple +/-1.
            var currentContainer = ItemView.ContainerFromIndex(currentIndex) as FrameworkElement;
            if (currentContainer is not null && currentContainer.ActualWidth != 0 && currentContainer.ActualHeight != 0)
            {
                // Use center of current container as reference
                var curPoint = currentContainer.TransformToVisual(ItemView).TransformPoint(new Point(0, 0));
                var curCenterX = curPoint.X + (currentContainer.ActualWidth / 2.0);
                var curCenterY = curPoint.Y + (currentContainer.ActualHeight / 2.0);

                var bestScore = double.MaxValue;
                var bestIndex = currentIndex;

                for (var i = 0; i < ItemView.Items.Count; i++)
                {
                    if (i == currentIndex)
                    {
                        continue;
                    }

                    if (IsSeparator(ItemView.Items[i]))
                    {
                        continue;
                    }

                    if (ItemView.ContainerFromIndex(i) is FrameworkElement c && c.ActualWidth > 0 && c.ActualHeight > 0)
                    {
                        var p = c.TransformToVisual(ItemView).TransformPoint(new Point(0, 0));
                        var centerX = p.X + (c.ActualWidth / 2.0);
                        var centerY = p.Y + (c.ActualHeight / 2.0);

                        var dx = centerX - curCenterX;
                        var dy = centerY - curCenterY;

                        var candidate = false;
                        var score = double.MaxValue;

                        switch (key)
                        {
                            case VirtualKey.Left:
                                if (dx < 0)
                                {
                                    candidate = true;
                                    score = Math.Abs(dy) + (Math.Abs(dx) * 0.7);
                                }

                                break;
                            case VirtualKey.Right:
                                if (dx > 0)
                                {
                                    candidate = true;
                                    score = Math.Abs(dy) + (Math.Abs(dx) * 0.7);
                                }

                                break;
                            case VirtualKey.Up:
                                if (dy < 0)
                                {
                                    candidate = true;
                                    score = Math.Abs(dx) + (Math.Abs(dy) * 0.7);
                                }

                                break;
                            case VirtualKey.Down:
                                if (dy > 0)
                                {
                                    candidate = true;
                                    score = Math.Abs(dx) + (Math.Abs(dy) * 0.7);
                                }

                                break;
                        }

                        if (candidate && score < bestScore)
                        {
                            bestScore = score;
                            bestIndex = i;
                        }
                    }
                }

                if (bestIndex != currentIndex)
                {
                    ItemView.SelectedIndex = bestIndex;
                    ItemView.ScrollIntoView(ItemView.SelectedItem);
                }

                return;
            }
        }
        catch
        {
            // ignore transform errors and fall back
        }

        // fallback linear behavior
        var fallback = key switch
        {
            VirtualKey.Left => Math.Max(0, currentIndex - 1),
            VirtualKey.Right => Math.Min(ItemView.Items.Count - 1, currentIndex + 1),
            VirtualKey.Up => Math.Max(0, currentIndex - 1),
            VirtualKey.Down => Math.Min(ItemView.Items.Count - 1, currentIndex + 1),
            _ => currentIndex,
        };
        if (fallback != currentIndex)
        {
            ItemView.SelectedIndex = fallback;
            ItemView.ScrollIntoView(ItemView.SelectedItem);
        }
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
        // Track keyboard as the last input source for activation logic.
        if (e.Key is VirtualKey.Enter or VirtualKey.Space)
        {
            _lastInputSource = InputSource.Keyboard;
            return;
        }

        // Handle arrow navigation when we're showing a grid.
        if (ViewModel?.IsGridView == true)
        {
            switch (e.Key)
            {
                case VirtualKey.Left:
                case VirtualKey.Right:
                case VirtualKey.Up:
                case VirtualKey.Down:
                    _lastInputSource = InputSource.Keyboard;
                    HandleGridArrowNavigation(e.Key);
                    e.Handled = true;
                    break;
            }
        }
    }

    /// <summary>
    ///  Code stealed from <see cref="Controls.ContextMenu.NavigateUp"/>
    /// </summary>
    private void NavigateUp()
    {
        var newIndex = ItemView.SelectedIndex;

        if (ItemView.SelectedIndex > 0)
        {
            newIndex--;

            while (
                newIndex >= 0 &&
                IsSeparator(ItemView.Items[newIndex]) &&
                newIndex != ItemView.SelectedIndex)
            {
                newIndex--;
            }

            if (newIndex < 0)
            {
                newIndex = ItemView.Items.Count - 1;

                while (
                    newIndex >= 0 &&
                    IsSeparator(ItemView.Items[newIndex]) &&
                    newIndex != ItemView.SelectedIndex)
                {
                    newIndex--;
                }
            }
        }
        else
        {
            newIndex = ItemView.Items.Count - 1;
        }

        ItemView.SelectedIndex = newIndex;
    }

    private void Items_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        try
        {
            if (e.Items.FirstOrDefault() is not ListItemViewModel item || item.DataPackage is null)
            {
                e.Cancel = true;
                return;
            }

            // copy properties
            foreach (var (key, value) in item.DataPackage.Properties)
            {
                try
                {
                    e.Data.Properties[key] = value;
                }
                catch (Exception)
                {
                    // noop - skip any properties that fail
                }
            }

            // setup e.Data formats as deferred renderers to read from the item's DataPackage
            foreach (var format in item.DataPackage.AvailableFormats)
            {
                try
                {
                    e.Data.SetDataProvider(format, request => DelayRenderer(request, item, format));
                }
                catch (Exception)
                {
                    // noop - skip any formats that fail
                }
            }

            WeakReferenceMessenger.Default.Send(new DragStartedMessage());
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new DragCompletedMessage());
            Logger.LogError("Failed to start dragging an item", ex);
        }
    }

    private static void DelayRenderer(DataProviderRequest request, ListItemViewModel item, string format)
    {
        var deferral = request.GetDeferral();
        try
        {
            item.DataPackage?.GetDataAsync(format)
                .AsTask()
                .ContinueWith(dataTask =>
                {
                    try
                    {
                        if (dataTask.IsCompletedSuccessfully)
                        {
                            request.SetData(dataTask.Result);
                        }
                        else if (dataTask.IsFaulted && dataTask.Exception is not null)
                        {
                            Logger.LogError($"Failed to get data for format '{format}' during drag-and-drop", dataTask.Exception);
                        }
                    }
                    finally
                    {
                        deferral.Complete();
                    }
                });
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to set data for format '{format}' during drag-and-drop", ex);
            deferral.Complete();
        }
    }

    private void Items_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        WeakReferenceMessenger.Default.Send(new DragCompletedMessage());
    }

    /// <summary>
    ///  Code stealed from <see cref="Controls.ContextMenu.NavigateDown"/>
    /// </summary>
    private void NavigateDown()
    {
        var newIndex = ItemView.SelectedIndex;

        if (ItemView.SelectedIndex == ItemView.Items.Count - 1)
        {
            newIndex = 0;
            while (
                newIndex < ItemView.Items.Count &&
                IsSeparator(ItemView.Items[newIndex]))
            {
                newIndex++;
            }

            if (newIndex >= ItemView.Items.Count)
            {
                return;
            }
        }
        else
        {
            newIndex++;

            while (
                newIndex < ItemView.Items.Count &&
                IsSeparator(ItemView.Items[newIndex]) &&
                newIndex != ItemView.SelectedIndex)
            {
                newIndex++;
            }

            if (newIndex >= ItemView.Items.Count)
            {
                newIndex = 0;

                while (
                    newIndex < ItemView.Items.Count &&
                    IsSeparator(ItemView.Items[newIndex]) &&
                    newIndex != ItemView.SelectedIndex)
                {
                    newIndex++;
                }
            }
        }

        ItemView.SelectedIndex = newIndex;
    }

    /// <summary>
    ///  Code stealed from <see cref="Controls.ContextMenu.IsSeparator(object)"/>
    /// </summary>
    private bool IsSeparator(object? item) => item is ListItemViewModel li && li.IsSectionOrSeparator;

    private enum InputSource
    {
        None,
        Keyboard,
        Pointer,
    }
}
