// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Dock;

public sealed partial class DockControl : UserControl, IRecipient<CloseContextMenuMessage>, IRecipient<EnterDockEditModeMessage>
{
    private DockViewModel _viewModel;

    internal DockViewModel ViewModel => _viewModel;

    public static readonly DependencyProperty ItemsOrientationProperty =
        DependencyProperty.Register(nameof(ItemsOrientation), typeof(Orientation), typeof(DockControl), new PropertyMetadata(Orientation.Horizontal));

    public Orientation ItemsOrientation
    {
        get => (Orientation)GetValue(ItemsOrientationProperty);
        set => SetValue(ItemsOrientationProperty, value);
    }

    public static readonly DependencyProperty DockSideProperty =
        DependencyProperty.Register(nameof(DockSide), typeof(DockSide), typeof(DockControl), new PropertyMetadata(DockSide.Top));

    public DockSide DockSide
    {
        get => (DockSide)GetValue(DockSideProperty);
        set => SetValue(DockSideProperty, value);
    }

    public static readonly DependencyProperty IsEditModeProperty =
        DependencyProperty.Register(nameof(IsEditMode), typeof(bool), typeof(DockControl), new PropertyMetadata(false, OnIsEditModeChanged));

    public bool IsEditMode
    {
        get => (bool)GetValue(IsEditModeProperty);
        set => SetValue(IsEditModeProperty, value);
    }

    private static void OnIsEditModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockControl control && e.NewValue is bool isEditMode)
        {
            control.UpdateEditMode(isEditMode);
        }
    }

    internal DockControl(DockViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        WeakReferenceMessenger.Default.Register<CloseContextMenuMessage>(this);
        WeakReferenceMessenger.Default.Register<EnterDockEditModeMessage>(this);

        // Start with edit mode disabled - normal click behavior
        UpdateEditMode(false);
    }

    public void Receive(EnterDockEditModeMessage message)
    {
        // Message may arrive from a background thread, dispatch to UI thread
        DispatcherQueue.TryEnqueue(() =>
        {
            EnterEditMode();
        });
    }

    private void UpdateEditMode(bool isEditMode)
    {
        // Enable/disable drag-and-drop based on edit mode
        StartListView.CanDragItems = isEditMode;
        StartListView.CanReorderItems = isEditMode;
        StartListView.AllowDrop = isEditMode;

        CenterListView.CanDragItems = isEditMode;
        CenterListView.CanReorderItems = isEditMode;
        CenterListView.AllowDrop = isEditMode;

        EndListView.CanDragItems = isEditMode;
        EndListView.CanReorderItems = isEditMode;
        EndListView.AllowDrop = isEditMode;

        if (isEditMode)
        {
            EditButtonsTeachingTip.PreferredPlacement = DockSide switch
            {
                DockSide.Left => TeachingTipPlacementMode.Right,
                DockSide.Right => TeachingTipPlacementMode.Left,
                DockSide.Top => TeachingTipPlacementMode.Bottom,
                DockSide.Bottom => TeachingTipPlacementMode.Top,
                _ => TeachingTipPlacementMode.Auto,
            };
        }

        EditButtonsTeachingTip.IsOpen = isEditMode;

        // Update visual state
        VisualStateManager.GoToState(this, isEditMode ? "EditModeOn" : "EditModeOff", true);
    }

    internal void EnterEditMode()
    {
        // Snapshot current state so we can restore on discard
        ViewModel.SnapshotBandOrder();
        IsEditMode = true;
    }

    internal void ExitEditMode()
    {
        IsEditMode = false;

        // Save all changes when exiting edit mode
        ViewModel.SaveBandOrder();
    }

    internal void DiscardEditMode()
    {
        IsEditMode = false;

        // Restore the original band order from snapshot
        ViewModel.RestoreBandOrder();
    }

    private void DoneEditingButton_Click(object sender, RoutedEventArgs e)
    {
        ExitEditMode();
    }

    private void DiscardEditingButton_Click(object sender, RoutedEventArgs e)
    {
        DiscardEditMode();
    }

    internal void UpdateSettings(DockSettings settings)
    {
        DockSide = settings.Side;

        var isHorizontal = settings.Side == DockSide.Top || settings.Side == DockSide.Bottom;

        ItemsOrientation = isHorizontal ? Orientation.Horizontal : Orientation.Vertical;

        if (settings.Backdrop == DockBackdrop.Transparent)
        {
            RootGrid.BorderBrush = new SolidColorBrush(Colors.Transparent);
        }
    }

    private void BandItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        // Ignore clicks when in edit mode - allow drag behavior instead
        if (IsEditMode)
        {
            return;
        }

        if (sender is DockItemControl dockItem && dockItem.DataContext is DockBandViewModel band && dockItem.Tag is DockItemViewModel item)
        {
            // Use the center of the border as the point to open at
            var borderPos = dockItem.TransformToVisual(null).TransformPoint(new Point(0, 0));
            var borderCenter = new Point(
                borderPos.X + (dockItem.ActualWidth / 2),
                borderPos.Y + (dockItem.ActualHeight / 2));

            // // borderCenter is in DIPs, relative to the dock window.
            // // we need screen DIPs
            // var windowPos = dockItem.XamlRoot.Content.XamlRoot.TransformToVisual(null).TransformPoint(new Point(0, 0));
            // var screenPos = new Point(
            //     borderCenter.X + windowPos.X,
            //     borderCenter.Y + windowPos.Y);
            InvokeItem(item, borderCenter);
            e.Handled = true;
        }
    }

    // Stores the band that was right-clicked for edit mode context menu
    private DockBandViewModel? _editModeContextBand;

    private void BandItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (sender is DockItemControl dockItem && dockItem.DataContext is DockBandViewModel band && dockItem.Tag is DockItemViewModel item)
        {
            // In edit mode, show the edit mode context menu (show/hide labels)
            if (IsEditMode)
            {
                // Find the parent DockBandViewModel for this item
                _editModeContextBand = band;
                if (_editModeContextBand != null)
                {
                    // Update toggle menu item checked state based on current settings
                    ShowTitlesMenuItem.IsChecked = _editModeContextBand.ShowTitles;
                    ShowSubtitlesMenuItem.IsChecked = _editModeContextBand.ShowSubtitles;

                    EditModeContextMenu.ShowAt(
                        dockItem,
                        new FlyoutShowOptions()
                        {
                            ShowMode = FlyoutShowMode.Standard,
                            Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                        });
                    e.Handled = true;
                }

                return;
            }

            // Normal mode - show the command context menu
            if (item.HasMoreCommands)
            {
                ContextControl.ViewModel.SelectedItem = item;
                ContextMenuFlyout.ShowAt(
                    dockItem,
                    new FlyoutShowOptions()
                    {
                        ShowMode = FlyoutShowMode.Standard,
                        Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                    });
                e.Handled = true;
            }
        }
    }

    private void ShowTitlesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_editModeContextBand != null)
        {
            _editModeContextBand.ShowTitles = ShowTitlesMenuItem.IsChecked;
        }
    }

    private void ShowSubtitlesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_editModeContextBand != null)
        {
            _editModeContextBand.ShowSubtitles = ShowSubtitlesMenuItem.IsChecked;
        }
    }

    private void UnpinBandMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_editModeContextBand != null)
        {
            ViewModel.UnpinBand(_editModeContextBand);
            _editModeContextBand = null;
        }
    }

    private void InvokeItem(DockItemViewModel item, Point pos)
    {
        var command = item.Command;
        try
        {
            // TODO! This is where we need to decide whether to open the command
            // as a context menu or as a full page.
            //
            // It might be the case that we should just have like... a
            // PerformDockCommandMessage like PerformCommandMessage but with the
            // context that we should be opening the command as a flyout.
            var m = PerformCommandMessage.CreateFlyoutMessage(command.Model, pos);
            WeakReferenceMessenger.Default.Send(m);

            // var isPage = command.Model.Unsafe is not IInvokableCommand invokable;
            // if (isPage)
            // {
            //     WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(pos));
            // }
        }
        catch (COMException e)
        {
            Logger.LogError("Error invoking dock command", e);
        }
    }

    private void ContextMenuFlyout_Opened(object sender, object e)
    {
        // We need to wait until our flyout is opened to try and toss focus
        // at its search box. The control isn't in the UI tree before that
        ContextControl.FocusSearchBox();
    }

    public void Receive(CloseContextMenuMessage message)
    {
        if (ContextMenuFlyout.IsOpen)
        {
            ContextMenuFlyout.Hide();
        }
    }

    private void RootGrid_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        var pos = e.GetPosition(null);
        var item = this.ViewModel.GetContextMenuForDock();
        if (item.HasMoreCommands)
        {
            ContextControl.ViewModel.SelectedItem = item;
            ContextMenuFlyout.ShowAt(
            this.RootGrid,
            new FlyoutShowOptions()
            {
                ShowMode = FlyoutShowMode.Standard,
                Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                Position = pos,
            });
            e.Handled = true;
        }
    }

    private DockBandViewModel? _draggedBand;

    private void BandListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count > 0 && e.Items[0] is DockBandViewModel band)
        {
            _draggedBand = band;
            e.Data.RequestedOperation = DataPackageOperation.Move;
        }
    }

    private void BandListView_DragOver(object sender, DragEventArgs e)
    {
        if (_draggedBand != null)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
        }
    }

    private void BandListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        // Reordering within the same list is handled automatically by ListView
        // We just need to sync the ViewModel order without saving
        if (args.DropResult == DataPackageOperation.Move && _draggedBand != null)
        {
            DockPinSide targetSide;
            ObservableCollection<DockBandViewModel> targetCollection;

            if (sender == StartListView)
            {
                targetSide = DockPinSide.Start;
                targetCollection = ViewModel.StartItems;
            }
            else if (sender == CenterListView)
            {
                targetSide = DockPinSide.Center;
                targetCollection = ViewModel.CenterItems;
            }
            else
            {
                targetSide = DockPinSide.End;
                targetCollection = ViewModel.EndItems;
            }

            // Find the new index and sync ViewModel (without saving)
            var newIndex = targetCollection.IndexOf(_draggedBand);
            if (newIndex >= 0)
            {
                ViewModel.SyncBandPosition(_draggedBand, targetSide, newIndex);
            }
        }

        _draggedBand = null;
    }

    private void StartListView_Drop(object sender, DragEventArgs e)
    {
        HandleCrossListDrop(DockPinSide.Start, e);
    }

    private void CenterListView_Drop(object sender, DragEventArgs e)
    {
        HandleCrossListDrop(DockPinSide.Center, e);
    }

    private void EndListView_Drop(object sender, DragEventArgs e)
    {
        HandleCrossListDrop(DockPinSide.End, e);
    }

    private void HandleCrossListDrop(DockPinSide targetSide, DragEventArgs e)
    {
        if (_draggedBand == null)
        {
            return;
        }

        // Check which list the band is currently in
        var isInStart = ViewModel.StartItems.Contains(_draggedBand);
        var isInCenter = ViewModel.CenterItems.Contains(_draggedBand);
        var isInEnd = ViewModel.EndItems.Contains(_draggedBand);

        DockPinSide sourceSide;
        if (isInStart)
        {
            sourceSide = DockPinSide.Start;
        }
        else if (isInCenter)
        {
            sourceSide = DockPinSide.Center;
        }
        else
        {
            sourceSide = DockPinSide.End;
        }

        // Only handle cross-list drops here; same-list reorders are handled in DragItemsCompleted
        if (sourceSide != targetSide)
        {
            // Calculate drop index based on drop position
            var targetListView = targetSide switch
            {
                DockPinSide.Start => StartListView,
                DockPinSide.Center => CenterListView,
                _ => EndListView,
            };
            var targetCollection = targetSide switch
            {
                DockPinSide.Start => ViewModel.StartItems,
                DockPinSide.Center => ViewModel.CenterItems,
                _ => ViewModel.EndItems,
            };

            var dropIndex = GetDropIndex(targetListView, e, targetCollection.Count);

            // Move the band to the new side (without saving - save happens on Done)
            ViewModel.MoveBandWithoutSaving(_draggedBand, targetSide, dropIndex);
            e.Handled = true;
        }
    }

    private int GetDropIndex(ListView listView, DragEventArgs e, int itemCount)
    {
        var position = e.GetPosition(listView);

        // Find the item at the drop position
        for (var i = 0; i < itemCount; i++)
        {
            if (listView.ContainerFromIndex(i) is ListViewItem container)
            {
                var itemBounds = container.TransformToVisual(listView).TransformBounds(
                    new Rect(0, 0, container.ActualWidth, container.ActualHeight));

                if (ItemsOrientation == Orientation.Horizontal)
                {
                    // For horizontal layout, check X position
                    if (position.X < itemBounds.X + (itemBounds.Width / 2))
                    {
                        return i;
                    }
                }
                else
                {
                    // For vertical layout, check Y position
                    if (position.Y < itemBounds.Y + (itemBounds.Height / 2))
                    {
                        return i;
                    }
                }
            }
        }

        // If we're past all items, insert at the end
        return itemCount;
    }

    // Tracks which section (Start/Center/End) the add button was clicked for
    private DockPinSide _addBandTargetSide;

    private void AddBandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string sideTag)
        {
            _addBandTargetSide = sideTag switch
            {
                "Start" => DockPinSide.Start,
                "Center" => DockPinSide.Center,
                "End" => DockPinSide.End,
                _ => DockPinSide.Center,
            };

            // Populate the list with available bands (not already in the dock)
            var availableBands = ViewModel.GetAvailableBandsToAdd().ToList();
            AddBandListView.ItemsSource = availableBands;

            // Show/hide empty state text based on whether there are bands to add
            var hasAvailableBands = availableBands.Count > 0;
            NoAvailableBandsText.Visibility = hasAvailableBands ? Visibility.Collapsed : Visibility.Visible;
            AddBandListView.Visibility = hasAvailableBands ? Visibility.Visible : Visibility.Collapsed;

            // Show the flyout
            AddBandFlyout.ShowAt(button);
        }
    }

    private void AddBandListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TopLevelViewModel topLevel)
        {
            // Add the band to the target section
            ViewModel.AddBandToSection(topLevel, _addBandTargetSide);

            // Close the flyout
            AddBandFlyout.Hide();
        }
    }
}
