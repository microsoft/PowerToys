// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace WindowsCommandPalette.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ListPage : Microsoft.UI.Xaml.Controls.Page, INotifyPropertyChanged
{
    public ListPageViewModel? ViewModel { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SolidColorBrush AccentColorBrush { get; set; } = new();

    private ListItemViewModel? _selectedItem;

    public ListItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            Debug.WriteLine($"      Selected: {SelectedItem?.Title}");
            _selectedItem = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MoreCommandsAvailable)));

            if (ViewModel != null && _selectedItem?.Details != null && ViewModel.ShowDetails)
            {
                this.DetailsContent.Child = new DetailsControl(_selectedItem.Details);
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

    private bool MoreCommandsAvailable => ItemsList.SelectedItem is not ListItemViewModel li ? false : li.HasMoreCommands;

    public ListPage()
    {
        this.InitializeComponent();

        this.ItemsList.Loaded += ItemsList_Loaded;
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

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = (ListPageViewModel?)e.Parameter;
        if (ViewModel == null)
        {
            return;
        }

        this.AccentColorBrush = new SolidColorBrush(ViewModel.AccentColor);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccentColorBrush)));

        if (e.NavigationMode == NavigationMode.New)
        {
            ViewModel.InitialRender().ContinueWith((t) =>
            {
                DispatcherQueue.TryEnqueue(() => { UpdateFilter(FilterBox.Text); });
                ViewModel.FilteredItems.CollectionChanged += FilteredItems_CollectionChanged;
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            });
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => { UpdateFilter(FilterBox.Text); });
        }

        this.ItemsList.SelectedIndex = 0;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ViewModel.SearchText):
                FilterBox.Select(FilterBox.Text.Length, 0);
                break;
        }
    }

    private void FilteredItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // *
        // Debug.WriteLine($"FilteredItems_CollectionChanged");
        // Try to maintain the selected item, if we can.
        if (ItemsList.SelectedItem != null &&
            ItemsList.SelectedItem is ListItemViewModel li)
        {
            var xamlListItem = ItemsList.ContainerFromItem(li);
            if (xamlListItem != null)
            {
                var index = ItemsList.IndexFromContainer(xamlListItem);
                if (index >= 0)
                {
                    // Debug.WriteLine("Found original selected item");
                    this.ItemsList.SelectedIndex = index;
                    return;
                }
            }
            else
            {
                // Debug.WriteLine($"Didn't find {li.Title} in new list");
            }
        }

        // */

        // Debug.WriteLine($"Selecting index 0");
        this.ItemsList.SelectedIndex = 0;
    }

    private void DoAction(ActionViewModel actionViewModel)
    {
        ViewModel?.DoAction(actionViewModel);
    }

    private void ListItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not ListViewItem listItem)
        {
            return;
        }

        if (listItem.DataContext is not ListItemViewModel li)
        {
            return;
        }

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
        var options = new FlyoutShowOptions
        {
            ShowMode = FlyoutShowMode.Standard,
        };

        MoreCommandsButton.Flyout.ShowAt(MoreCommandsButton, options);
        ActionsDropdown.SelectedIndex = 0;
        ActionsDropdown.Focus(FocusState.Programmatic);
    }

    private void ListItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not ListViewItem listItem)
        {
            return;
        }

        if (listItem.DataContext is not ListItemViewModel li)
        {
            return;
        }

        _ = li;

        // For a bit I had double-clicks Invoke and single just select, but that crashes?
        // ItemsList.SelectedItem = listItem;
        if (li.DefaultAction != null)
        {
            DoAction(new(li.DefaultAction));
        }
    }

    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Debug.WriteLine($"    ItemsList_SelectionChanged");
        if (sender is not ListView lv)
        {
            return;
        }

        if (lv.SelectedItem is not ListItemViewModel li)
        {
            return;
        }

        SelectedItem = li;
    }

    private void ActionListViewItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not ListViewItem listItem)
        {
            return;
        }

        if (listItem.DataContext is not ContextItemViewModel vm)
        {
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Enter || e.Key == Windows.System.VirtualKey.Space)
        {
            DoAction(new(vm.Command));
            e.Handled = true;
        }
    }

    private void ActionListViewItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not ListViewItem listItem)
        {
            return;
        }

        if (listItem.DataContext is not ContextItemViewModel vm)
        {
            return;
        }

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
        if (e.Handled)
        {
            return;
        }

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
        else if (e.Key == Windows.System.VirtualKey.Right)
        {
            if (!string.IsNullOrEmpty(SelectedItem?.TextToSuggest))
            {
                FilterBox.Text = SelectedItem.TextToSuggest;
                FilterBox.Select(SelectedItem.TextToSuggest.Length, 0);
                FilterBox.Focus(FocusState.Keyboard);
            }
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
                Debug.WriteLine("Clear seearch text");
                FilterBox.Text = string.Empty;
            }
            else
            {
                ViewModel?.GoBack();
            }

            e.Handled = true;
        } // ctrl+k
        else if (ctrlPressed && e.Key == Windows.System.VirtualKey.K && ActionsDropdown.Items.Count > 0)
        {
            var options = new FlyoutShowOptions
            {
                ShowMode = FlyoutShowMode.Standard,
            };
            MoreCommandsButton.Flyout.ShowAt(MoreCommandsButton, options);
            ActionsDropdown.SelectedIndex = 0;
            ActionsDropdown.Focus(FocusState.Programmatic);
        }
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        // on the UI thread
        UpdateFilter(FilterBox.Text);
    }

    private void UpdateFilter(string text)
    {
        if (ViewModel == null)
        {
            return;
        }

        // Debug.WriteLine($"UpdateFilter({text})");
        ViewModel.UpdateSearchText(text);
    }

    private void BackButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel?.GoBack();
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        InstallationDialog.Visibility = InstallationDialog.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }
}
