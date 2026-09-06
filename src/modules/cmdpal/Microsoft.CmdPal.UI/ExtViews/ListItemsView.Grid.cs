// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Windows.Foundation;
using Windows.System;

namespace Microsoft.CmdPal.UI;

public sealed partial class ListItemsView
{
    private readonly Queue<VirtualKey> _pendingGridNavigation = new();
    private GridNavigationLayout? _gridNavigationLayout;
    private long _gridNavigationVersion = -1;
    private int _gridColumns;
    private double _gridItemHeight;
    private int? _gridNavigationColumn;
    private bool _isGridNavigationSelection;
    private bool _gridSynchronizationQueued;
    private bool _gridLayoutTracked;
    private bool _processingGridLayout;
    private bool _focusGridNavigationTarget;
    private ListItemViewModel? _pendingGridHeaderScroll;

    public GridItemsViewModel GridItems { get; private set; } = new();

    public CollectionViewSource GridItemsSource { get; } = new()
    {
        IsSourceGrouped = true,
        ItemsPath = new PropertyPath(nameof(GridItemGroupViewModel.Items)),
    };

    private double GridHeaderHeight => (double)Resources["ListViewSectionHeight"] + (double)Resources["GridItemSpacing"];

    private DataTemplate GetGridItemTemplate(IGridPropertiesViewModel? properties)
        => (DataTemplate)Resources[properties switch
        {
            SmallGridPropertiesViewModel => "SmallGridItemViewModelTemplate",
            GalleryGridPropertiesViewModel => "GalleryGridItemViewModelTemplate",
            _ => "MediumGridItemViewModelTemplate",
        }];

    private Style GetGridItemStyle(IGridPropertiesViewModel? properties)
        => (Style)Resources[properties is GalleryGridPropertiesViewModel ? "GalleryGridViewItemStyle" : "IconGridViewItemStyle"];

    private void Grid_ChoosingGroupHeaderContainer(ListViewBase sender, ChoosingGroupHeaderContainerEventArgs args)
    {
        if (args.Group is GridItemGroupViewModel group)
        {
            // Bind the actual native header, including when it is recycled.
            // Its automation peer does not use the header template's name.
            args.GroupHeaderContainer ??= new GridViewHeaderItem();
            args.GroupHeaderContainer.SetBinding(AutomationProperties.NameProperty, new Binding
            {
                Source = group,
                Path = new PropertyPath(nameof(GridItemGroupViewModel.Title)),
                Mode = BindingMode.OneWay,
            });
        }
    }

    private bool SynchronizeGridItems()
    {
        var source = ViewModel?.IsGridView == true ? ViewModel.FilteredItems : null;
        bool changed;
        using (SuppressSelectionChangedScope())
        {
            if (source is null)
            {
                changed = ReleaseGridProjection();
            }
            else
            {
                GridItems.SetSource(source);
                var projection = GridItems;
                changed = projection.Synchronize();
                if (!ReferenceEquals(projection, GridItems))
                {
                    return false;
                }

                // Only reattach once the panel is back, otherwise the next
                // synchronization would resume sending removals to a torn-down
                // one. Items_Loaded synchronizes again and attaches then.
                if (GridItemsSource.Source is null && ItemsGrid.IsLoaded)
                {
                    GridItemsSource.Source = projection.Groups;
                }
            }
        }

        if (!changed)
        {
            return false;
        }

        _gridNavigationLayout = null;
        _gridNavigationColumn = null;
        return true;
    }

    /// <summary>
    /// Drops the projection the native panel is bound to. The panel discards its
    /// group cache as soon as it unloads or its ItemsSource is cleared, while the
    /// grouped view stays subscribed, so a later group removal faults inside the
    /// native group bookkeeping. Releasing the view and the projection together
    /// leaves nothing that could touch those collections again, rather than
    /// relying on the discarded view having let go of them.
    /// </summary>
    /// <returns>Whether there was still a projection to release.</returns>
    private bool ReleaseGridProjection()
    {
        CancelPendingGridActions();
        if (_selectedGridSectionCommand is not null)
        {
            SetSectionCommandSelection(_selectedListSectionCommand, null);
        }

        if (GridItemsSource.Source is null && GridItems.Groups.Count == 0)
        {
            // Already released. Still stop observing: an empty grid page reaches
            // here with a live source and nothing to tear down.
            GridItems.SetSource(null);
            return false;
        }

        GridItemsSource.Source = null;
        GridItems.Invalidated -= GridItems_Invalidated;
        GridItems.Dispose();
        GridItems = new();
        GridItems.Invalidated += GridItems_Invalidated;
        return true;
    }

    private void GridItems_Invalidated(object? sender, EventArgs e)
    {
        if (!_isLoaded || _gridSynchronizationQueued)
        {
            return;
        }

        _gridSynchronizationQueued = true;
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            _gridSynchronizationQueued = false;
            if (_isLoaded && ViewModel is { IsGridView: true } vm && GridItems.HasPendingChanges)
            {
                // The regular ItemsUpdated path normally synchronizes first.
                // This path also covers late header initialization/property changes.
                var version = Volatile.Read(ref _itemsUpdatedVersion);
                ProcessItemsUpdated(vm, new ItemsUpdatedEventArgs(forceFirstItem: false, ensureSelectionVisible: false), version);
            }
        }))
        {
            _gridSynchronizationQueued = false;
        }
    }

    private bool TryGetGridNavigationLayout(out GridNavigationLayout? layout)
    {
        layout = null;
        if (ItemsGrid.ItemsPanelRoot is not ItemsWrapGrid panel || panel.ActualWidth <= 0)
        {
            return false;
        }

        // Sample a prepared container in the viewport. Cached containers can
        // retain old geometry after their content has been cleared for recycling.
        // Never generate a container to locate an offscreen item.
        FrameworkElement? sample = null;
        var last = Math.Min(panel.LastVisibleIndex, ItemsGrid.Items.Count - 1);
        for (var i = Math.Max(0, panel.FirstVisibleIndex); i <= last; i++)
        {
            if (ItemsGrid.ContainerFromIndex(i) is GridViewItem { IsLoaded: true, Content: ListItemViewModel, ActualWidth: > 0, ActualHeight: > 0 } container &&
                ReferenceEquals(container.Content, ItemsGrid.Items[i]))
            {
                sample = container;
                break;
            }
        }

        if (sample is null)
        {
            return false;
        }

        var width = sample.ActualWidth + sample.Margin.Left + sample.Margin.Right;
        var height = sample.ActualHeight + sample.Margin.Top + sample.Margin.Bottom;
        var availableWidth = panel.ActualWidth - panel.GroupPadding.Left - panel.GroupPadding.Right;
        var columns = Math.Max(1, (int)Math.Floor((availableWidth + 0.0001) / width));
        if (panel.MaximumRowsOrColumns > 0)
        {
            columns = Math.Min(columns, panel.MaximumRowsOrColumns);
        }

        if (_gridNavigationLayout is null || _gridNavigationVersion != GridItems.Version ||
            _gridColumns != columns || _gridItemHeight != height)
        {
            _gridNavigationLayout = new GridNavigationLayout(GridItems.Groups, columns, height, GridHeaderHeight);
            _gridNavigationVersion = GridItems.Version;
            _gridColumns = columns;
            _gridItemHeight = height;
            _gridNavigationColumn = null;
        }

        layout = _gridNavigationLayout;
        return true;
    }

    private void HandleGridArrowNavigation(VirtualKey key)
    {
        if (ViewModel?.IsGridView != true)
        {
            return;
        }

        SynchronizeGridItems();
        if (GridItems.ItemCount == 0)
        {
            TryNavigateHeaderOnlyGrid(key);
            return;
        }

        if (_pendingGridNavigation.Count > 0 || !TryNavigateGrid(key))
        {
            _pendingGridNavigation.Enqueue(key);
            TrackGridLayout();
            if (ItemsGrid.Items.Count > 0)
            {
                ItemsGrid.ScrollIntoView(ItemsGrid.SelectedItem ?? ItemsGrid.Items[0]);
            }
        }
    }

    private bool TryNavigateGrid(VirtualKey key)
    {
        if (ItemsGrid.Items.Count != GridItems.ItemCount || GridItems.HasPendingChanges)
        {
            return false;
        }

        if (_selectedGridSectionCommand is not null)
        {
            if (!GridItems.Groups.Contains(_selectedGridSectionCommand) || !_selectedGridSectionCommand.HasSectionCommand)
            {
                SetSectionCommandSelection(null, null);
            }
            else
            {
                return TryNavigateFromGridSectionCommand(_selectedGridSectionCommand, key);
            }
        }

        if (GridItems.ItemCount == 0)
        {
            return TryNavigateHeaderOnlyGrid(key);
        }

        var current = ItemsGrid.SelectedIndex;
        var target = current;
        if (current < 0)
        {
            target = 0;
        }
        else if (key is VirtualKey.Left or VirtualKey.Right)
        {
            var increaseIndex = key == VirtualKey.Right;
            if (ItemsGrid.FlowDirection == FlowDirection.RightToLeft)
            {
                increaseIndex = !increaseIndex;
            }

            target = GridNavigationLayout.MoveHorizontal(current, increaseIndex, GridItems.ItemCount);
            _gridNavigationColumn = null;
        }
        else
        {
            if (!TryGetGridNavigationLayout(out var layout) || layout is null)
            {
                return false;
            }

            var viewportHeight = CurrentScrollViewer?.ViewportHeight ?? 0;
            if (key is VirtualKey.PageUp or VirtualKey.PageDown &&
                (!double.IsFinite(viewportHeight) || viewportHeight <= 0))
            {
                return false;
            }

            var column = _gridNavigationColumn ??= layout.GetColumn(current);

            if (key is VirtualKey.Up or VirtualKey.Down &&
                GridItems.GroupFromItemIndex(current) is { } currentGroup)
            {
                var relativeIndex = current - currentGroup.FirstItemIndex;
                var row = relativeIndex / _gridColumns;
                var rowCount = 1 + ((currentGroup.Items.Count - 1) / _gridColumns);

                if (key == VirtualKey.Up && row == 0)
                {
                    return TrySelectGridTargetBeforeTiles(currentGroup, column, wrap: true);
                }

                if (key == VirtualKey.Down && row == rowCount - 1)
                {
                    return TrySelectGridTargetAfterGroup(currentGroup, column, wrap: true);
                }
            }

            target = key switch
            {
                VirtualKey.Up => layout.MoveVertical(current, down: false, column, wrap: true),
                VirtualKey.Down => layout.MoveVertical(current, down: true, column, wrap: true),
                VirtualKey.PageUp => layout.MovePage(current, down: false, column, viewportHeight),
                VirtualKey.PageDown => layout.MovePage(current, down: true, column, viewportHeight),
                _ => current,
            };
        }

        if (target >= 0 && target != current)
        {
            SelectGridItem(target);
        }

        return true;
    }

    private bool TryNavigateFromGridSectionCommand(GridItemGroupViewModel group, VirtualKey key)
    {
        var column = _gridNavigationColumn ?? 0;
        return key switch
        {
            VirtualKey.Up or VirtualKey.PageUp => TrySelectGridTargetBeforeGroup(group, column, wrap: true),
            VirtualKey.Down or VirtualKey.PageDown when group.Items.Count > 0 => SelectGridItemInFirstRow(group, column),
            VirtualKey.Down or VirtualKey.PageDown => TrySelectGridTargetAfterGroup(group, column, wrap: true),
            VirtualKey.Left or VirtualKey.Right => true,
            _ => true,
        };
    }

    private bool TryNavigateHeaderOnlyGrid(VirtualKey key)
    {
        if (key is not (VirtualKey.Up or VirtualKey.Down or VirtualKey.PageUp or VirtualKey.PageDown))
        {
            return true;
        }

        var down = key is VirtualKey.Down or VirtualKey.PageDown;
        var selectedIndex = _selectedGridSectionCommand is null ? -1 : IndexOfGridGroup(_selectedGridSectionCommand);
        if (selectedIndex < 0)
        {
            return TrySelectGridSectionCommand(down ? 0 : GridItems.Groups.Count - 1, down ? 1 : -1, 0);
        }

        return TrySelectGridSectionCommand(selectedIndex + (down ? 1 : -1), down ? 1 : -1, 0, wrap: true);
    }

    private bool TrySelectGridTargetBeforeTiles(GridItemGroupViewModel group, int column, bool wrap)
    {
        if (group.HasSectionCommand)
        {
            return SelectGridSectionCommand(group, column);
        }

        return TrySelectGridTargetBeforeGroup(group, column, wrap);
    }

    private bool TrySelectGridTargetBeforeGroup(GridItemGroupViewModel group, int column, bool wrap)
    {
        var groupIndex = IndexOfGridGroup(group);
        if (groupIndex < 0)
        {
            return false;
        }

        for (var i = groupIndex - 1; i >= 0; i--)
        {
            if (TrySelectLastTargetInGridGroup(GridItems.Groups[i], column))
            {
                return true;
            }
        }

        if (wrap)
        {
            for (var i = GridItems.Groups.Count - 1; i >= groupIndex; i--)
            {
                if (TrySelectLastTargetInGridGroup(GridItems.Groups[i], column))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TrySelectGridTargetAfterGroup(GridItemGroupViewModel group, int column, bool wrap)
    {
        var groupIndex = IndexOfGridGroup(group);
        if (groupIndex < 0)
        {
            return false;
        }

        for (var i = groupIndex + 1; i < GridItems.Groups.Count; i++)
        {
            if (TrySelectFirstTargetInGridGroup(GridItems.Groups[i], column))
            {
                return true;
            }
        }

        if (wrap)
        {
            for (var i = 0; i <= groupIndex; i++)
            {
                if (TrySelectFirstTargetInGridGroup(GridItems.Groups[i], column))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TrySelectFirstTargetInGridGroup(GridItemGroupViewModel group, int column)
    {
        if (group.HasSectionCommand)
        {
            return SelectGridSectionCommand(group, column);
        }

        return group.Items.Count > 0 && SelectGridItemInFirstRow(group, column);
    }

    private bool TrySelectLastTargetInGridGroup(GridItemGroupViewModel group, int column)
    {
        if (group.Items.Count > 0)
        {
            var columns = Math.Max(1, _gridColumns);
            var lastRowStart = group.FirstItemIndex + (((group.Items.Count - 1) / columns) * columns);
            var index = lastRowStart + Math.Min(column, group.Items.Count - (lastRowStart - group.FirstItemIndex) - 1);
            return SelectGridItem(index);
        }

        return group.HasSectionCommand && SelectGridSectionCommand(group, column);
    }

    private bool SelectGridItemInFirstRow(GridItemGroupViewModel group, int column)
    {
        var index = group.FirstItemIndex + Math.Min(column, group.Items.Count - 1);
        return SelectGridItem(index);
    }

    private bool SelectGridItem(int target)
    {
        if (target < 0 || target >= ItemsGrid.Items.Count)
        {
            return false;
        }

        SetSectionCommandSelection(null, null);
        _isGridNavigationSelection = true;
        try
        {
            _scrollOnNextSelectionChange = true;
            ItemsGrid.SelectedIndex = target;
        }
        finally
        {
            _isGridNavigationSelection = false;
        }

        PushSelectionToVm();
        return true;
    }

    private bool SelectGridSectionCommand(GridItemGroupViewModel group, int column)
    {
        if (!group.HasSectionCommand || group.Header is null)
        {
            return false;
        }

        using (SuppressSelectionChangedScope())
        {
            ItemsGrid.SelectedIndex = -1;
        }

        SetSectionCommandSelection(null, group);
        _gridNavigationColumn = column;
        _stickySelectedItem = group.Header;
        _lastPushedToVm = null;
        _scrollOnNextSelectionChange = false;
        ViewModel?.UpdateSelectedItemCommand.Execute(null);
        AnnounceSelection($"{group.Title}, {group.SectionCommandName}");
        ScrollGridToSectionCommand(group);
        return true;
    }

    private bool TrySelectGridSectionCommand(int startIndex, int step, int column, bool wrap = false)
    {
        if (step == 0 || GridItems.Groups.Count == 0)
        {
            return false;
        }

        for (var i = startIndex; i >= 0 && i < GridItems.Groups.Count; i += step)
        {
            if (SelectGridSectionCommand(GridItems.Groups[i], column))
            {
                return true;
            }
        }

        if (!wrap)
        {
            return false;
        }

        var wrappedStart = step > 0 ? 0 : GridItems.Groups.Count - 1;
        var wrappedEnd = Math.Clamp(startIndex - step, 0, GridItems.Groups.Count - 1);
        for (var i = wrappedStart; step > 0 ? i <= wrappedEnd : i >= wrappedEnd; i += step)
        {
            if (SelectGridSectionCommand(GridItems.Groups[i], column))
            {
                return true;
            }
        }

        return false;
    }

    private int IndexOfGridGroup(GridItemGroupViewModel group)
    {
        for (var i = 0; i < GridItems.Groups.Count; i++)
        {
            if (ReferenceEquals(GridItems.Groups[i], group))
            {
                return i;
            }
        }

        return -1;
    }

    private void ScrollGridToSectionCommand(GridItemGroupViewModel group)
    {
        if (group.Items.Count == 0)
        {
            return;
        }

        var firstItem = group.Items[0];
        _pendingGridHeaderScroll = firstItem;
        TrackGridLayout();
        ItemsGrid.ScrollIntoView(firstItem);
    }

    private void ScrollGridToItem(ListItemViewModel item)
    {
        _pendingGridHeaderScroll = null;
        var index = ReferenceEquals(ItemsGrid.SelectedItem, item) ? ItemsGrid.SelectedIndex : GridItems.IndexOf(item);
        var group = GridItems.GroupFromItemIndex(index);
        if (group is { HasHeader: true } && group.FirstItemIndex == index)
        {
            _pendingGridHeaderScroll = item;
            TrackGridLayout();
        }

        ItemsGrid.ScrollIntoView(item);
    }

    private void TrackGridLayout()
    {
        if (!_gridLayoutTracked)
        {
            ItemsGrid.LayoutUpdated += Grid_LayoutUpdated;
            _gridLayoutTracked = true;
        }
    }

    private void RequestGridNavigationTargetFocus()
    {
        _focusGridNavigationTarget = true;
        if (_pendingGridNavigation.Count == 0)
        {
            TryFocusGridNavigationTarget();
        }

        if (_focusGridNavigationTarget)
        {
            TrackGridLayout();
        }
    }

    private void TryFocusGridNavigationTarget()
    {
        if (!_focusGridNavigationTarget)
        {
            return;
        }

        Control? target;
        if (_selectedGridSectionCommand is { } group)
        {
            target = ItemsGrid.FindDescendants()
                .OfType<Button>()
                .FirstOrDefault(button => ReferenceEquals(button.DataContext, group));
        }
        else if (ItemsGrid.SelectedIndex >= 0 && ItemsGrid.SelectedIndex < ItemsGrid.Items.Count)
        {
            target = ItemsGrid.ContainerFromIndex(ItemsGrid.SelectedIndex) as GridViewItem;
        }
        else
        {
            _focusGridNavigationTarget = false;
            return;
        }

        if (target?.Focus(FocusState.Keyboard) == true)
        {
            _focusGridNavigationTarget = false;
        }
    }

    private void Grid_LayoutUpdated(object? sender, object e)
    {
        if (_processingGridLayout)
        {
            return;
        }

        _processingGridLayout = true;
        try
        {
            while (_pendingGridNavigation.TryPeek(out var key) && TryNavigateGrid(key))
            {
                _pendingGridNavigation.Dequeue();
            }

            if (_pendingGridHeaderScroll is ListItemViewModel item)
            {
                if (GridItems.IndexOf(item) < 0)
                {
                    _pendingGridHeaderScroll = null;
                }
                else if (ItemsGrid.ContainerFromItem(item) is GridViewItem { IsLoaded: true, Content: ListItemViewModel content, ActualHeight: > 0 } container &&
                    ReferenceEquals(content, item))
                {
                    _pendingGridHeaderScroll = null;

                    // The item can be offscreen when requested. Once realized,
                    // bring a rectangle including its header into the viewport.
                    container.StartBringIntoView(new BringIntoViewOptions
                    {
                        TargetRect = new Rect(0, -GridHeaderHeight, container.ActualWidth, container.ActualHeight + GridHeaderHeight),
                    });
                }
            }

            TryFocusGridNavigationTarget();

            if (_pendingGridNavigation.Count == 0 && _pendingGridHeaderScroll is null && !_focusGridNavigationTarget)
            {
                StopTrackingGridLayout();
            }
        }
        finally
        {
            _processingGridLayout = false;
        }
    }

    private void StopTrackingGridLayout()
    {
        if (_gridLayoutTracked)
        {
            ItemsGrid.LayoutUpdated -= Grid_LayoutUpdated;
            _gridLayoutTracked = false;
        }
    }

    private void CancelPendingGridActions()
    {
        StopTrackingGridLayout();
        _pendingGridNavigation.Clear();
        _pendingGridHeaderScroll = null;
        _focusGridNavigationTarget = false;
        _gridNavigationColumn = null;
        _gridNavigationLayout = null;
    }
}
