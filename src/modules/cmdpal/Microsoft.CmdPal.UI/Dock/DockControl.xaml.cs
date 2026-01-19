// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Dock;

#pragma warning disable SA1402 // File may only contain a single type

public sealed partial class DockControl : UserControl, INotifyPropertyChanged, IRecipient<CloseContextMenuMessage>, IRecipient<EnterDockEditModeMessage>
{
    private DockViewModel _viewModel;

    internal DockViewModel ViewModel => _viewModel;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Orientation ItemsOrientation
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(ItemsOrientation)));
            }
        }
    }

    public DockSide DockSide
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(DockSide)));
            }
        }
    }

    public bool IsEditMode
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(IsEditMode)));
                UpdateEditMode(value);
            }
        }
    }

    public double IconSize
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(IconSize)));
                PropertyChanged?.Invoke(this, new(nameof(IconMinWidth)));
            }
        }
    }

= 16.0;

    public double IconMinWidth => IconSize / 2;

    public double TitleTextMaxWidth
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(TitleTextMaxWidth)));
            }
        }
    }

= 100;

    public double TitleTextFontSize
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(TitleTextFontSize)));
            }
        }
    }

= 12;

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
        StartItemsListView.CanDragItems = isEditMode;
        StartItemsListView.CanReorderItems = isEditMode;
        StartItemsListView.AllowDrop = isEditMode;

        EndItemsListView.CanDragItems = isEditMode;
        EndItemsListView.CanReorderItems = isEditMode;
        EndItemsListView.AllowDrop = isEditMode;

        // Update visual state
        VisualStateManager.GoToState(this, isEditMode ? "EditModeOn" : "EditModeOff", true);
    }

    internal void EnterEditMode()
    {
        IsEditMode = true;
    }

    internal void ExitEditMode()
    {
        IsEditMode = false;
    }

    private void DoneEditingButton_Click(object sender, RoutedEventArgs e)
    {
        ExitEditMode();
    }

    internal void UpdateSettings(DockSettings settings)
    {
        DockSide = settings.Side;

        var isHorizontal = settings.Side == DockSide.Top || settings.Side == DockSide.Bottom;

        ItemsOrientation = isHorizontal ? Orientation.Horizontal : Orientation.Vertical;

        IconSize = DockSettingsToViews.IconSizeForSize(settings.DockIconsSize);
        TitleTextFontSize = DockSettingsToViews.TitleTextFontSizeForSize(settings.DockSize);
        TitleTextMaxWidth = DockSettingsToViews.TitleTextMaxWidthForSize(settings.DockSize);

        if (settings.Backdrop == DockBackdrop.Transparent)
        {
            RootGrid.BorderBrush = new SolidColorBrush(Colors.Transparent);
        }
    }

    private void BandItem_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = (Brush)Application.Current.Resources["TaskBarButtonBackgroundPointerOver"];
        }
    }

    private void BandItem_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new SolidColorBrush(Colors.Transparent);
        }
    }

    private void BandItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        // Ignore clicks when in edit mode - allow drag behavior instead
        if (IsEditMode)
        {
            return;
        }

        var border = sender as Border;
        var item = border?.DataContext as DockItemViewModel;

        if (item is null || border is null)
        {
            return;
        }

        // Use the center of the border as the point to open at
        var borderPos = border.TransformToVisual(null).TransformPoint(new Point(0, 0));
        var borderCenter = new Point(
            borderPos.X + (border.ActualWidth / 2),
            borderPos.Y + (border.ActualHeight / 2));

        InvokeItem(item, borderCenter);
        e.Handled = true;
    }

    private void BandItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        // Ignore right-clicks when in edit mode
        if (IsEditMode)
        {
            return;
        }

        var border = sender as Border;
        var item = border?.DataContext as DockItemViewModel;

        if (item is null || border is null)
        {
            return;
        }

        if (item.HasMoreCommands)
        {
            ContextControl.ViewModel.SelectedItem = item;
            ContextMenuFlyout.ShowAt(
                border,
                new FlyoutShowOptions()
                {
                    ShowMode = FlyoutShowMode.Standard,
                    Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                });
            e.Handled = true;
        }
    }

    private void InvokeItem(DockItemViewModel item, global::Windows.Foundation.Point pos)
    {
        var command = item.Command;
        try
        {
            var isPage = command.Model.Unsafe is not IInvokableCommand invokable;
            if (isPage)
            {
                WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(pos));
            }

            PerformCommandMessage m = new(command.Model);
            m.WithAnimation = false;
            m.TransientPage = true;
            WeakReferenceMessenger.Default.Send(m);
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

    internal HorizontalAlignment GetBandAlignment(ObservableCollection<DockItemViewModel> items)
    {
        if (DockSide == DockSide.Top || DockSide == DockSide.Bottom)
        {
            return HorizontalAlignment.Center;
        }

        var requestedTheme = ActualTheme;
        var isLight = requestedTheme == Microsoft.UI.Xaml.ElementTheme.Light;

        // Check if any of the items have both an icon and a label.
        //
        // If so, left align so that the icons don't wobble if the text
        // changes.
        //
        // Otherwise, center align.
        foreach (var item in items)
        {
            var showText = item.ShowLabel && item.HasText;
            var showIcon = item.Icon is not null && item.Icon.HasIcon(isLight);
            if (showText && showIcon)
            {
                return HorizontalAlignment.Left;
            }
        }

        return HorizontalAlignment.Center;
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
        // This handles reordering within the same list
        if (args.DropResult == DataPackageOperation.Move && _draggedBand != null)
        {
            var isStartList = sender == StartItemsListView;
            var targetSide = isStartList ? DockPinSide.Start : DockPinSide.End;
            var targetCollection = isStartList ? ViewModel.StartItems : ViewModel.EndItems;

            // Find the new index of the dragged band
            var newIndex = targetCollection.IndexOf(_draggedBand);
            if (newIndex >= 0)
            {
                ViewModel.MoveBand(_draggedBand, targetSide, newIndex);
            }
        }

        _draggedBand = null;
    }

    private void StartItemsListView_Drop(object sender, DragEventArgs e)
    {
        HandleCrossListDrop(DockPinSide.Start, e);
    }

    private void EndItemsListView_Drop(object sender, DragEventArgs e)
    {
        HandleCrossListDrop(DockPinSide.End, e);
    }

    private void HandleCrossListDrop(DockPinSide targetSide, DragEventArgs e)
    {
        if (_draggedBand == null)
        {
            return;
        }

        // Check if this is a cross-list drop (dragging from the other list)
        var isInStart = ViewModel.StartItems.Contains(_draggedBand);
        var isInEnd = ViewModel.EndItems.Contains(_draggedBand);

        var sourceIsStart = isInStart;
        var targetIsStart = targetSide == DockPinSide.Start;

        // Only handle cross-list drops here; same-list reorders are handled in DragItemsCompleted
        if (sourceIsStart != targetIsStart)
        {
            // Calculate drop index based on drop position
            var targetListView = targetIsStart ? StartItemsListView : EndItemsListView;
            var targetCollection = targetIsStart ? ViewModel.StartItems : ViewModel.EndItems;

            var dropIndex = GetDropIndex(targetListView, e, targetCollection.Count);

            // Move the band to the new side
            ViewModel.MoveBand(_draggedBand, targetSide, dropIndex);
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
}

internal sealed partial class BandAlignmentConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public DockControl? Control { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ObservableCollection<DockItemViewModel> items && Control is not null)
        {
            return Control.GetBandAlignment(items);
        }

        return HorizontalAlignment.Center;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

#pragma warning restore SA1402 // File may only contain a single type
