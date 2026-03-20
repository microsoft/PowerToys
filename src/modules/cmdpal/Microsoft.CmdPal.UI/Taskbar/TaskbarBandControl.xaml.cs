// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Dock;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Taskbar;

public sealed partial class TaskbarBandControl : UserControl,
    IRecipient<CloseContextMenuMessage>,
    IRecipient<EnterEditModeMessage>,
    IRecipient<ExitEditModeMessage>
{
    private DockViewModel _viewModel;
    private bool _isEditMode;
    private bool _showFlyout;
    private DockBandViewModel? _editModeContextBand;
    private DockBandViewModel? _draggedBand;

    internal DockViewModel ViewModel => _viewModel;

    internal TaskbarBandControl(DockViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();

        BandsListView.ItemsSource = _viewModel.TaskbarItems;

        WeakReferenceMessenger.Default.Register<CloseContextMenuMessage>(this);
        WeakReferenceMessenger.Default.Register<EnterEditModeMessage>(this);
        WeakReferenceMessenger.Default.Register<ExitEditModeMessage>(this);

        UpdateEditMode(false);
    }

    public void SetMaxAvailableWidth(double availableSpace)
    {
        if (availableSpace <= 0)
        {
            MoreButton.Visibility = Visibility.Collapsed;
            BandsListView.Visibility = Visibility.Collapsed;
            return;
        }

        BandsListView.Visibility = Visibility.Visible;

        // Measure how much space the bands need
        double neededSpace = 0;
        var items = _viewModel.TaskbarItems;
        var visibleBands = new List<DockBandViewModel>();
        var overflowBands = new List<DockBandViewModel>();

        foreach (var band in items)
        {
            if (BandsListView.ContainerFromItem(band) is FrameworkElement container)
            {
                container.Measure(new Size(availableSpace, ActualHeight));
                var needed = container.DesiredSize.Width;
                neededSpace += needed;
            }
        }

        if (neededSpace <= availableSpace)
        {
            // Everything fits
            MoreButton.Visibility = Visibility.Collapsed;
            OverflowListView.ItemsSource = null;
        }
        else
        {
            // Some items need to overflow
            MoreButton.Visibility = Visibility.Visible;
            double moreButtonWidth = 40; // approximate
            double takenSpace = moreButtonWidth;

            foreach (var band in items)
            {
                if (BandsListView.ContainerFromItem(band) is FrameworkElement container)
                {
                    var needed = container.DesiredSize.Width;
                    if (takenSpace + needed > availableSpace)
                    {
                        overflowBands.Add(band);
                    }
                    else
                    {
                        visibleBands.Add(band);
                        takenSpace += needed;
                    }
                }
            }

            OverflowListView.ItemsSource = overflowBands;
        }
    }

    internal void EnterEditMode(bool showFlyout = true)
    {
        _showFlyout = showFlyout;
        _viewModel.SnapshotBandOrder();
        _isEditMode = true;
        UpdateEditMode(true, showFlyout);
    }

    internal void ExitEditMode()
    {
        _isEditMode = false;
        UpdateEditMode(false);
        _viewModel.SaveBandOrder();
    }

    internal void DiscardEditMode()
    {
        _isEditMode = false;
        UpdateEditMode(false);
        _viewModel.RestoreBandOrder();
    }

    private void UpdateEditMode(bool isEditMode, bool showFlyout = true)
    {
        BandsListView.CanDragItems = isEditMode;
        BandsListView.CanReorderItems = isEditMode;
        BandsListView.AllowDrop = isEditMode;

        AddBandButton.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;
        EditButtonsTeachingTip.IsOpen = isEditMode && showFlyout;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Parent (TaskbarWindow) will manage available width via SetMaxAvailableWidth
    }

    private void BandItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (_isEditMode)
        {
            return;
        }

        if (sender is DockItemControl dockItem && dockItem.DataContext is DockBandViewModel band && dockItem.Tag is DockItemViewModel item)
        {
            var borderPos = dockItem.TransformToVisual(null).TransformPoint(new Point(0, 0));
            var borderCenter = new Point(
                borderPos.X + (dockItem.ActualWidth / 2),
                borderPos.Y + (dockItem.ActualHeight / 2));

            InvokeItem(item, borderCenter);
            e.Handled = true;
        }
    }

    private void BandItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (sender is DockItemControl dockItem && dockItem.DataContext is DockBandViewModel band && dockItem.Tag is DockItemViewModel item)
        {
            if (_isEditMode)
            {
                _editModeContextBand = band;
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
                return;
            }

            if (item.HasMoreCommands)
            {
                ContextControl.ViewModel.SelectedItem = item;
                ContextControl.ShowFilterBox = true;
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
            _viewModel.UnpinBand(_editModeContextBand);
            _editModeContextBand = null;
        }
    }

    private void InvokeItem(DockItemViewModel item, Point pos)
    {
        var command = item.Command;
        try
        {
            PerformCommandMessage m = new(command.Model);
            m.WithAnimation = false;
            m.TransientPage = true;
            WeakReferenceMessenger.Default.Send(m);

            var isPage = command.Model.Unsafe is not IInvokableCommand;
            if (isPage)
            {
                WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(pos));
            }
        }
        catch (COMException e)
        {
            Logger.LogError("Error invoking taskbar command", e);
        }
    }

    private void ContextMenuFlyout_Opened(object sender, object e)
    {
        ContextControl.FocusSearchBox();
    }

    public void Receive(CloseContextMenuMessage message)
    {
        if (ContextMenuFlyout.IsOpen)
        {
            ContextMenuFlyout.Hide();
        }
    }

    public void Receive(EnterEditModeMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            EnterEditMode(showFlyout: message.Origin == EditModeOrigin.Taskbar);
        });
    }

    public void Receive(ExitEditModeMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (message.Save)
            {
                ExitEditMode();
            }
            else
            {
                DiscardEditMode();
            }
        });
    }

    private void RootPanel_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (_isEditMode)
        {
            return;
        }

        var pos = e.GetPosition(null);
        var item = _viewModel.GetContextMenuForTaskbar();
        if (item.HasMoreCommands)
        {
            ContextControl.ViewModel.SelectedItem = item;
            ContextControl.ShowFilterBox = false;
            ContextMenuFlyout.ShowAt(
                (FrameworkElement)sender,
                new FlyoutShowOptions()
                {
                    ShowMode = FlyoutShowMode.Standard,
                    Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                    Position = e.GetPosition((UIElement)sender),
                });
            e.Handled = true;
        }
    }

    private void DoneEditingButton_Click(object sender, RoutedEventArgs e)
    {
        // Tell both dock and taskbar to exit edit mode
        WeakReferenceMessenger.Default.Send(new ExitEditModeMessage(Save: true));
    }

    private void DiscardEditingButton_Click(object sender, RoutedEventArgs e)
    {
        // Tell both dock and taskbar to discard edit mode
        WeakReferenceMessenger.Default.Send(new ExitEditModeMessage(Save: false));
    }

    private void AddBandButton_Click(object sender, RoutedEventArgs e)
    {
        var availableBands = _viewModel.GetAvailableBandsToAdd().ToList();
        AddBandListView.ItemsSource = availableBands;

        var hasAvailableBands = availableBands.Count > 0;
        NoAvailableBandsText.Visibility = hasAvailableBands ? Visibility.Collapsed : Visibility.Visible;
        AddBandListView.Visibility = hasAvailableBands ? Visibility.Visible : Visibility.Collapsed;

        AddBandFlyout.ShowAt((Button)sender);
    }

    private void AddBandListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TopLevelViewModel topLevel)
        {
            _viewModel.AddBandToSection(topLevel, DockPinSide.Taskbar);
            AddBandFlyout.Hide();
        }
    }

    // Drag and drop handlers
    private void BandsListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count > 0 && e.Items[0] is DockBandViewModel band)
        {
            _draggedBand = band;
            _viewModel.DraggedBand = band;
            e.Data.RequestedOperation = DataPackageOperation.Move;
        }
    }

    private void BandsListView_DragOver(object sender, DragEventArgs e)
    {
        // Accept drops from this window (_draggedBand) or from the dock
        // window (shared via _viewModel.DraggedBand)
        if (_draggedBand != null || _viewModel.DraggedBand != null)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
        }
    }

    private void BandsListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (args.DropResult == DataPackageOperation.Move && _draggedBand != null)
        {
            var newIndex = _viewModel.TaskbarItems.IndexOf(_draggedBand);
            if (newIndex >= 0)
            {
                _viewModel.SyncBandPosition(_draggedBand, DockPinSide.Taskbar, newIndex);
            }
        }

        _draggedBand = null;
        _viewModel.DraggedBand = null;
    }

    private void BandsListView_Drop(object sender, DragEventArgs e)
    {
        // Use local _draggedBand for same-window drags, fall back to shared
        // _viewModel.DraggedBand for cross-window drags (e.g. from dock)
        var draggedBand = _draggedBand ?? _viewModel.DraggedBand;
        if (draggedBand == null)
        {
            return;
        }

        // Only handle cross-section drops; same-list reorders are handled in DragItemsCompleted
        if (!_viewModel.TaskbarItems.Contains(draggedBand))
        {
            var dropIndex = GetDropIndex(BandsListView, e, _viewModel.TaskbarItems.Count);
            _viewModel.MoveBandWithoutSaving(draggedBand, DockPinSide.Taskbar, dropIndex);
            e.Handled = true;
        }

        ResetListViewState(sender);
    }

    private int GetDropIndex(ListView listView, DragEventArgs e, int itemCount)
    {
        var position = e.GetPosition(listView);

        for (var i = 0; i < itemCount; i++)
        {
            if (listView.ContainerFromIndex(i) is ListViewItem container)
            {
                var itemBounds = container.TransformToVisual(listView).TransformBounds(
                    new Rect(0, 0, container.ActualWidth, container.ActualHeight));

                // Horizontal layout: check X position
                if (position.X < itemBounds.X + (itemBounds.Width / 2))
                {
                    return i;
                }
            }
        }

        return itemCount;
    }

    private void BandsListView_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is ListView view)
        {
            view.Background = Application.Current.Resources["ControlAltFillColorQuarternaryBrush"] as Microsoft.UI.Xaml.Media.SolidColorBrush;
            e.DragUIOverride.IsGlyphVisible = false;
            e.DragUIOverride.IsCaptionVisible = false;
        }
    }

    private void BandsListView_DragLeave(object sender, DragEventArgs e)
    {
        ResetListViewState(sender);
    }

    private void ResetListViewState(object sender)
    {
        if (sender is ListView view)
        {
            view.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }
}
