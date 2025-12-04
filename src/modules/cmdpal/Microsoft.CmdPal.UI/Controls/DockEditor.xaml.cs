// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class DockEditor : UserControl
{
    private DockBandSettingsViewModel? _draggedItem;
    private ObservableCollection<DockBandSettingsViewModel>? _sourceCollection;
    private DockEditorPinArea _targetArea;
    private DockBandSettingsViewModel? _currentContextItem;

    /// <summary>
    /// Gets or sets the collection of all dock band items to display and manage.
    /// </summary>
    public IList<DockBandSettingsViewModel> DockItems
    {
        get => (IList<DockBandSettingsViewModel>)GetValue(DockItemsProperty);
        set => SetValue(DockItemsProperty, value);
    }

    public static readonly DependencyProperty DockItemsProperty =
        DependencyProperty.Register(
            nameof(DockItems),
            typeof(IList<DockBandSettingsViewModel>),
            typeof(DockEditor),
            new PropertyMetadata(new ObservableCollection<DockBandSettingsViewModel>(), OnDockItemsChanged));

    /// <summary>
    /// Gets items pinned to the start (left) area of the dock.
    /// </summary>
    public ObservableCollection<DockBandSettingsViewModel> StartItems { get; } = new();

    /// <summary>
    /// Gets items pinned to the center area of the dock (UI concept, stored as Start).
    /// </summary>
    public ObservableCollection<DockBandSettingsViewModel> CenterItems { get; } = new();

    /// <summary>
    /// Gets items pinned to the end (right) area of the dock.
    /// </summary>
    public ObservableCollection<DockBandSettingsViewModel> EndItems { get; } = new();

    /// <summary>
    /// Gets items available to be added to the dock (not currently pinned).
    /// </summary>
    public ObservableCollection<DockBandSettingsViewModel> AvailableItems { get; } = new();

    public DockEditor()
    {
        InitializeComponent();
    }

    private static void OnDockItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockEditor editor)
        {
            editor.LoadItems();
        }
    }

    private void LoadItems()
    {
        StartItems.Clear();
        CenterItems.Clear();
        EndItems.Clear();

        if (DockItems == null)
        {
            return;
        }

        foreach (var item in DockItems)
        {
            // Use PinSideIndex: 0 = None, 1 = Start, 2 = End
            switch (item.PinSideIndex)
            {
                case 1: // Start
                    StartItems.Add(item);
                    break;
                case 2: // End
                    EndItems.Add(item);
                    break;
                default: // None (0) - available for adding
                    break;
            }
        }

        RefreshAvailableItems();
    }

    private void RefreshAvailableItems()
    {
        AvailableItems.Clear();

        if (DockItems == null)
        {
            return;
        }

        foreach (var item in DockItems)
        {
            // Items with PinSideIndex == 0 (None) are available
            if (item.PinSideIndex == 0)
            {
                AvailableItems.Add(item);
            }
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        // Determine which area to add to based on button name
        _targetArea = button.Name switch
        {
            "StartAddButton" => DockEditorPinArea.Start,
            "CenterAddButton" or "CenterAddButtonLeft" => DockEditorPinArea.Center,
            "EndAddButton" => DockEditorPinArea.End,
            _ => DockEditorPinArea.None,
        };

        // Refresh available items before showing flyout
        RefreshAvailableItems();

        // Show the flyout
        var flyout = button.Flyout as Flyout;
        flyout?.ShowAt(button);
    }

    private void AvailableItemsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not DockBandSettingsViewModel selectedItem)
        {
            return;
        }

        // Set the pin side based on target area
        // PinSideIndex: 0 = None, 1 = Start, 2 = End
        selectedItem.PinSideIndex = _targetArea switch
        {
            DockEditorPinArea.Start => 1,
            DockEditorPinArea.Center => 1, // Center maps to Start
            DockEditorPinArea.End => 2,
            _ => 0,
        };

        // Add to the appropriate UI collection
        var targetCollection = _targetArea switch
        {
            DockEditorPinArea.Start => StartItems,
            DockEditorPinArea.Center => CenterItems,
            DockEditorPinArea.End => EndItems,
            _ => null,
        };

        targetCollection?.Add(selectedItem);

        // Refresh available items
        RefreshAvailableItems();

        // Close the flyout
        CloseFlyoutFromSender(sender);
    }

    private void DockItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not DockBandSettingsViewModel clickedItem)
        {
            return;
        }

        _currentContextItem = clickedItem;

        // Update the flyout title
        OptionsFlyoutTitle.Text = clickedItem.Title;

        // Set the combo box to the current show labels setting
        ShowLabelsComboBox.SelectedIndex = clickedItem.ShowLabelsIndex;

        // Show the flyout attached to the clicked item
        if (sender is ListView listView)
        {
            var container = listView.ContainerFromItem(clickedItem) as ListViewItem;
            if (container != null && Resources.TryGetValue("DockItemOptionsFlyout", out var flyoutResource) && flyoutResource is Flyout flyout)
            {
                flyout.ShowAt(container);
            }
        }
    }

    private void ShowLabelsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_currentContextItem == null || sender is not ComboBox comboBox)
        {
            return;
        }

        _currentContextItem.ShowLabelsIndex = comboBox.SelectedIndex;
    }

    private void UnpinButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentContextItem == null)
        {
            return;
        }

        // Remove from all UI collections
        StartItems.Remove(_currentContextItem);
        CenterItems.Remove(_currentContextItem);
        EndItems.Remove(_currentContextItem);

        // Set pin side to None
        _currentContextItem.PinSideIndex = 0;

        // Refresh available items
        RefreshAvailableItems();

        _currentContextItem = null;

        // Close the flyout
        CloseFlyoutFromSender(sender);
    }

    private void DockItemOptionsFlyout_Closed(object sender, object e)
    {
        _currentContextItem = null;
    }

    private void ListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count > 0 && e.Items[0] is DockBandSettingsViewModel item)
        {
            _draggedItem = item;
            _sourceCollection = (sender as ListView)?.ItemsSource as ObservableCollection<DockBandSettingsViewModel>;
            e.Data.RequestedOperation = DataPackageOperation.Move;
        }
    }

    private void ListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        // Reordering within the same list is handled automatically by ListView
        // The PinSideIndex doesn't need to change
        _draggedItem = null;
        _sourceCollection = null;
    }

    private void ListView_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Move;
        e.DragUIOverride.Caption = "Move";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsContentVisible = true;
        e.DragUIOverride.IsGlyphVisible = true;
    }

    private void StartListView_Drop(object sender, DragEventArgs e)
    {
        HandleDrop(sender, DockEditorPinArea.Start, e);
    }

    private void CenterListView_Drop(object sender, DragEventArgs e)
    {
        HandleDrop(sender, DockEditorPinArea.Center, e);
    }

    private void EndListView_Drop(object sender, DragEventArgs e)
    {
        HandleDrop(sender, DockEditorPinArea.End, e);
    }

    private void HandleDrop(object sender, DockEditorPinArea targetArea, DragEventArgs e)
    {
        if (_draggedItem == null)
        {
            return;
        }

        var targetListView = sender as ListView;
        var targetCollection = targetListView?.ItemsSource as ObservableCollection<DockBandSettingsViewModel>;

        if (targetCollection == null || _sourceCollection == null)
        {
            return;
        }

        // If dropping to a different collection, move the item
        if (targetCollection != _sourceCollection)
        {
            // Remove from source collection
            _sourceCollection.Remove(_draggedItem);

            // Calculate the drop index based on the position
            var dropIndex = GetDropIndex(targetListView!, e);

            // Insert at the drop position
            if (dropIndex >= targetCollection.Count)
            {
                targetCollection.Add(_draggedItem);
            }
            else
            {
                targetCollection.Insert(dropIndex, _draggedItem);
            }

            // Update the pin side
            // PinSideIndex: 0 = None, 1 = Start, 2 = End
            _draggedItem.PinSideIndex = targetArea switch
            {
                DockEditorPinArea.Start => 1,
                DockEditorPinArea.Center => 1, // Center maps to Start
                DockEditorPinArea.End => 2,
                _ => 0,
            };
        }

        // Clear references
        _draggedItem = null;
        _sourceCollection = null;
    }

    private int GetDropIndex(ListView listView, DragEventArgs e)
    {
        var position = e.GetPosition(listView);
        var items = listView.ItemsSource as ObservableCollection<DockBandSettingsViewModel>;

        if (items == null || items.Count == 0)
        {
            return 0;
        }

        // Find the drop position based on X coordinate for horizontal layout
        for (var i = 0; i < items.Count; i++)
        {
            var container = listView.ContainerFromIndex(i) as ListViewItem;
            if (container != null)
            {
                var containerPosition = container.TransformToVisual(listView).TransformPoint(new Point(0, 0));
                var containerCenter = containerPosition.X + (container.ActualWidth / 2);

                if (position.X < containerCenter)
                {
                    return i;
                }
            }
        }

        return items.Count;
    }

    private static void CloseFlyoutFromSender(object sender)
    {
        // Walk up the visual tree to find and close the flyout
        var element = sender as DependencyObject;
        while (element != null)
        {
            if (element is Popup popup)
            {
                popup.IsOpen = false;
                return;
            }

            if (element is FlyoutPresenter)
            {
                // Find the parent popup
                var parent = VisualTreeHelper.GetParent(element);
                while (parent != null && parent is not Popup)
                {
                    parent = VisualTreeHelper.GetParent(parent);
                }

                if (parent is Popup flyoutPopup)
                {
                    flyoutPopup.IsOpen = false;
                }

                return;
            }

            element = VisualTreeHelper.GetParent(element);
        }
    }
}

/// <summary>
/// Pin area enum for the DockEditor UI.
/// </summary>
public enum DockEditorPinArea
{
    None,
    Start,
    Center,
    End,
}
