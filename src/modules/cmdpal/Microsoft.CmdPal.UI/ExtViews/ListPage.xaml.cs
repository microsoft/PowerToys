// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
using Microsoft.UI.Xaml.Automation.Peers;
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

    private int _itemsUpdatedVersion;
    private bool _suppressSelectionChanged;

    private bool _scrollOnNextSelectionChange;

    private ListItemViewModel? _stickySelectedItem;
    private ListItemViewModel? _lastPushedToVm;

    internal ListViewModel? ViewModel
    {
        get => (ListViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ListViewModel), typeof(ListPage), new PropertyMetadata(null, OnViewModelChanged));

    public ShellViewModel ShellViewModel
    {
        get => (ShellViewModel)GetValue(ShellViewModelProperty);
        set => SetValue(ShellViewModelProperty, value);
    }

    public static readonly DependencyProperty ShellViewModelProperty = DependencyProperty.Register(nameof(ShellViewModel), typeof(ShellViewModel), typeof(ListPage), new PropertyMetadata(null, OnShellViewModelChanged));

    private ListViewBase ItemView => ViewModel?.IsGridView == true ? ItemsGrid : ItemsList;

    public ListPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Disabled;
        ItemView.Loaded += Items_Loaded;
        ItemView.PreviewKeyDown += Items_PreviewKeyDown;
        ItemView.PointerPressed += Items_PointerPressed;
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
        ShellViewModel = navigationRequest.ShellViewModel;
        PokeTemplateSelector();

        if (e.NavigationMode == NavigationMode.Back)
        {
            // Must dispatch the selection to run at a lower priority; otherwise, GetFirstSelectableIndex
            // may return an incorrect index because item containers are not yet rendered.
            _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                // Only do this if we truly have no selection.
                if (ItemView.SelectedItem is null)
                {
                    var firstUsefulIndex = GetFirstSelectableIndex();
                    if (firstUsefulIndex != -1)
                    {
                        using (SuppressSelectionChangedScope())
                        {
                            ItemView.SelectedIndex = firstUsefulIndex;
                        }
                    }
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
                // Click-driven selection should scroll into view (but only once).
                _scrollOnNextSelectionChange = true;

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
        if (_suppressSelectionChanged)
        {
            return;
        }

        var vm = ViewModel;
        var li = ItemView.SelectedItem as ListItemViewModel;

        // Transient null/separator selection can happen during in-place updates.
        // Do not push null into the VM; Page_ItemsUpdated will repair selection.
        if (li is null || IsSeparator(li))
        {
            return;
        }

        _stickySelectedItem = li;

        // Do not Task.Run (it reorders selection updates).
        vm?.UpdateSelectedItemCommand.Execute(li);

        // Only scroll when explicitly requested by navigation/click handlers.
        if (_scrollOnNextSelectionChange)
        {
            _scrollOnNextSelectionChange = false;
            ItemView.ScrollIntoView(li);
        }

        // Automation notification for screen readers
        var listViewPeer = ListViewAutomationPeer.CreatePeerForElement(ItemView);
        if (listViewPeer is not null)
        {
            UIHelper.AnnounceActionForAccessibility(
                ItemsList,
                li.Title,
                "CommandPaletteSelectedItemChanged");
        }
    }

    private void Items_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement element &&
            element.DataContext is ListItemViewModel item)
        {
            if (ItemView.SelectedItem != item)
            {
                _scrollOnNextSelectionChange = true;

                using (SuppressSelectionChangedScope())
                {
                    ItemView.SelectedItem = item;
                }
            }

            ViewModel?.UpdateSelectedItemCommand.Execute(item);

            var pos = e.GetPosition(element);

            _ = DispatcherQueue.TryEnqueue(
                () =>
                {
                    WeakReferenceMessenger.Default.Send<OpenContextMenuMessage>(
                        new OpenContextMenuMessage(
                            element,
                            FlyoutPlacementMode.BottomEdgeAlignedLeft,
                            pos,
                            ContextMenuFilterLocation.Top));
                });
        }
    }

    private void Items_Loaded(object sender, RoutedEventArgs e)
    {
        // Find the ScrollViewer in the ItemView (ItemsList or ItemsGrid)
        var listViewScrollViewer = FindScrollViewer(ItemView);

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

        if (scrollView.VerticalOffset >= (scrollView.ScrollableHeight * .8))
        {
            ViewModel?.LoadMoreIfNeeded();
        }
    }

    // Message-driven navigation should count as keyboard.
    private void MarkKeyboardNavigation() => _lastInputSource = InputSource.Keyboard;

    private void PushSelectionToVm()
    {
        if (ViewModel is null || ItemView.SelectedItem is not ListItemViewModel li || IsSeparator(li))
        {
            return;
        }

        if (ReferenceEquals(_lastPushedToVm, li))
        {
            return;
        }

        _lastPushedToVm = li;
        _stickySelectedItem = li;
        ViewModel.UpdateSelectedItemCommand.Execute(li);
    }

    public void Receive(NavigateNextCommand message)
    {
        MarkKeyboardNavigation();
        _scrollOnNextSelectionChange = true;

        if (ViewModel?.IsGridView == true)
        {
            HandleGridArrowNavigation(VirtualKey.Down);
        }
        else
        {
            NavigateDown();
        }

        PushSelectionToVm();
    }

    public void Receive(NavigatePreviousCommand message)
    {
        MarkKeyboardNavigation();
        _scrollOnNextSelectionChange = true;

        if (ViewModel?.IsGridView == true)
        {
            HandleGridArrowNavigation(VirtualKey.Up);
        }
        else
        {
            NavigateUp();
        }

        PushSelectionToVm();
    }

    public void Receive(NavigateLeftCommand message)
    {
        MarkKeyboardNavigation();
        _scrollOnNextSelectionChange = true;

        if (ViewModel?.IsGridView == true)
        {
            HandleGridArrowNavigation(VirtualKey.Left);
            PushSelectionToVm();
        }
    }

    public void Receive(NavigateRightCommand message)
    {
        MarkKeyboardNavigation();
        _scrollOnNextSelectionChange = true;

        if (ViewModel?.IsGridView == true)
        {
            HandleGridArrowNavigation(VirtualKey.Right);
            PushSelectionToVm();
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
        MarkKeyboardNavigation();
        _scrollOnNextSelectionChange = true;

        var indexes = CalculateTargetIndexPageUpDownScrollTo(true);
        if (indexes is null)
        {
            return;
        }

        if (indexes.Value.CurrentIndex != indexes.Value.TargetIndex)
        {
            ItemView.SelectedIndex = indexes.Value.TargetIndex;
        }

        PushSelectionToVm();
    }

    public void Receive(NavigatePageUpCommand message)
    {
        MarkKeyboardNavigation();
        _scrollOnNextSelectionChange = true;

        var indexes = CalculateTargetIndexPageUpDownScrollTo(false);
        if (indexes is null)
        {
            return;
        }

        if (indexes.Value.CurrentIndex != indexes.Value.TargetIndex)
        {
            ItemView.SelectedIndex = indexes.Value.TargetIndex;
        }

        PushSelectionToVm();
    }

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
                }
            }
        }

        var itemsPerPage = 0;

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

    private static void OnShellViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListPage @this)
        {
            if (e.OldValue is ShellViewModel old)
            {
                old.PropertyChanged -= @this.ShellViewModel_PropertyChanged;
            }

            if (e.NewValue is ShellViewModel shellViewModel)
            {
                shellViewModel.PropertyChanged += @this.ShellViewModel_PropertyChanged;
            }
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
        var version = Interlocked.Increment(ref _itemsUpdatedVersion);
        var forceFirstItem = args is true;

        // Try to handle selection immediately — items should already be available
        // since FilteredItems is a direct ObservableCollection bound as ItemsSource.
        if (!TrySetSelectionAfterUpdate(sender, version, forceFirstItem))
        {
            // Fallback: binding hasn't propagated yet, defer to next tick.
            _ = DispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () =>
                {
                    if (version != Volatile.Read(ref _itemsUpdatedVersion))
                    {
                        return;
                    }

                    TrySetSelectionAfterUpdate(sender, version, forceFirstItem);
                });
        }
    }

    /// <summary>
    /// Applies selection after an items update. Returns false if ItemView.Items
    /// is not yet populated (caller should defer and retry).
    /// </summary>
    /// <param name="forceFirstItem">
    /// When true, always select the first selectable item and scroll to top
    /// (used for filter changes and top-level fetches).
    /// </param>
    private bool TrySetSelectionAfterUpdate(ListViewModel sender, long version, bool forceFirstItem)
    {
        if (version != Volatile.Read(ref _itemsUpdatedVersion))
        {
            return true; // superseded by a newer update, nothing to do
        }

        var vm = ViewModel;
        if (vm is null)
        {
            return true;
        }

        // Use the stable source of truth, not ItemView.Items (which can be transiently empty)
        if (vm.FilteredItems.Count == 0)
        {
            using (SuppressSelectionChangedScope())
            {
                ItemView.SelectedIndex = -1;
                _stickySelectedItem = null;
                _lastPushedToVm = null;
            }

            return true;
        }

        // If ItemView.Items hasn't caught up with the ObservableCollection yet,
        // signal the caller to defer and retry.
        var items = ItemView.Items;
        if (items is null || items.Count == 0)
        {
            return false;
        }

        var firstUsefulIndex = GetFirstSelectableIndex();
        if (firstUsefulIndex == -1)
        {
            using (SuppressSelectionChangedScope())
            {
                ItemView.SelectedIndex = -1;
                _stickySelectedItem = null;
                _lastPushedToVm = null;
            }

            return true;
        }

        var shouldUpdateSelection = forceFirstItem;

        if (!shouldUpdateSelection)
        {
            // Check if selection needs repair (item gone, null, or separator).
            if (ItemView.SelectedItem is null)
            {
                shouldUpdateSelection = true;
            }
            else if (IsSeparator(ItemView.SelectedItem))
            {
                shouldUpdateSelection = true;
            }
            else if (!items.Contains(ItemView.SelectedItem))
            {
                shouldUpdateSelection = true;
            }
        }

        if (shouldUpdateSelection)
        {
            using (SuppressSelectionChangedScope())
            {
                if (!forceFirstItem &&
                    _stickySelectedItem is not null &&
                    items.Contains(_stickySelectedItem) &&
                    !IsSeparator(_stickySelectedItem))
                {
                    // Preserve sticky selection for nested dynamic updates.
                    ItemView.SelectedItem = _stickySelectedItem;
                }
                else
                {
                    // Select the first interactive item.
                    ItemView.SelectedItem = items[firstUsefulIndex];
                }

                // Prevent any pending "scroll on selection" logic from fighting this.
                _scrollOnNextSelectionChange = false;

                _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (version != Volatile.Read(ref _itemsUpdatedVersion))
                    {
                        return;
                    }

                    ResetScrollToTop();
                });
            }
        }

        PushSelectionToVm();
        return true;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var prop = e.PropertyName;
        if (prop == nameof(ViewModel.GridProperties))
        {
            PokeTemplateSelector();
        }
    }

    private void ShellViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var prop = e.PropertyName;
        if (prop == nameof(ShellViewModel.IsDetailsVisible))
        {
            PokeTemplateSelector();
        }
    }

    private void PokeTemplateSelector()
    {
        if (Resources["ListItemTemplateSelector"] is not ListItemTemplateSelector selector)
        {
            return;
        }

        var newMode = ComputeListItemViewMode();
        if (selector.ListItemViewMode == newMode)
        {
            return;
        }

        var selected = ItemView.SelectedItem;

        selector.ListItemViewMode = newMode;

        using (SuppressSelectionChangedScope())
        {
            ItemsList.ItemTemplateSelector = null;
            ItemsList.ItemTemplateSelector = selector;

            // Restore if still present; Page_ItemsUpdated will fix if not.
            if (selected is not null)
            {
                ItemsList.SelectedItem = selected;
            }
        }

        return;

        ListItemViewMode ComputeListItemViewMode()
        {
            var useCompact =
                ViewModel?.GridProperties is SinglelineListPropertiesViewModel p &&
                IsCompactFeasible(p, ShellViewModel);

            return useCompact ? ListItemViewMode.Singleline : ListItemViewMode.Multiline;
        }

        static bool IsCompactFeasible(SinglelineListPropertiesViewModel propertiesViewModel, ShellViewModel shellViewModel)
        {
            if (!shellViewModel.IsDetailsVisible)
            {
                return true;
            }

            if (!propertiesViewModel.IsAutomaticWrappingEnabled)
            {
                return true;
            }

            if (shellViewModel.Details is null)
            {
                return true;
            }

            return shellViewModel.Details.Size < propertiesViewModel.AutomaticWrappingBreakpoint;
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
            return;
        }

        var currentIndex = ItemView.SelectedIndex;
        if (currentIndex < 0)
        {
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
            _scrollOnNextSelectionChange = true;

            using (SuppressSelectionChangedScope())
            {
                ItemView.SelectedItem = item;
            }
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
                        FlyoutPlacementMode.BottomEdgeAlignedLeft,
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
                    _scrollOnNextSelectionChange = true;
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

    private bool IsSeparator(object? item) => item is ListItemViewModel li && !li.IsInteractive;

    private void ResetScrollToTop()
    {
        var scroll = FindScrollViewer(ItemView);
        if (scroll is null)
        {
            return;
        }

        // disableAnimation: true prevents a visible jump animation
        scroll.ChangeView(horizontalOffset: null, verticalOffset: 0, zoomFactor: null, disableAnimation: true);
    }

    private IDisposable SuppressSelectionChangedScope()
    {
        _suppressSelectionChanged = true;
        return new ActionOnDispose(() => _suppressSelectionChanged = false);
    }

    private sealed partial class ActionOnDispose(Action action) : IDisposable
    {
        public void Dispose() => action();
    }

    private enum InputSource
    {
        None,
        Keyboard,
        Pointer,
    }
}
