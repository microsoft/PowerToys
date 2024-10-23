// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using WindowsCommandPalette;

namespace WindowsCommandPalette.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ListPage : Microsoft.UI.Xaml.Controls.Page, INotifyPropertyChanged
{
    public ListPageViewModel? ViewModel { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private ListItemViewModel? _selectedItem;

    public ListItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
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

    private bool MoreCommandsAvailable
    {
        get
        {
            if (ItemsList.SelectedItem is not ListItemViewModel li)
            {
                return false;
            }

            return li.HasMoreCommands;
        }
    }

    public ListPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = (ListPageViewModel?)e.Parameter;
        if (ViewModel == null)
        {
            return;
        }

        if (e.NavigationMode == NavigationMode.New)
        {
            ViewModel.InitialRender().ContinueWith((t) =>
            {
                DispatcherQueue.TryEnqueue(async () => { await UpdateFilter(FilterBox.Text); });
            });
        }
        else
        {
            DispatcherQueue.TryEnqueue(async () => { await UpdateFilter(FilterBox.Text); });
        }

        this.ItemsCVS.Source = ViewModel?.FilteredItems;
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
        FlyoutShowOptions options = new FlyoutShowOptions
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
            FlyoutShowOptions options = new FlyoutShowOptions
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
        _ = UpdateFilter(FilterBox.Text);
    }

    private async Task UpdateFilter(string text)
    {
        if (ViewModel == null)
        {
            return;
        }

        // ViewModel.Query = text;

        // This first part will first filter all the commands that were passed
        // into us initially. We handle the filtering of these ones. Commands
        // from async querying happens later.
        var newMatches = await ViewModel.GetFilteredItems(text);

        // this.ItemsCVS.Source = ViewModel.FilteredItems;
        // Returns back on the UI thread
        ListHelpers.InPlaceUpdateList(ViewModel.FilteredItems, newMatches);

        /*
        // for (var i = 0; i < ViewModel.FilteredItems.Count && i < newMatches.Count; i++)
        // {
        //     for (var j = i; j < ViewModel.FilteredItems.Count; j++)
        //     {
        //         if (ViewModel.FilteredItems[j] == newMatches[i])
        //         {
        //             for (var k = i; k < j; k++)
        //             {
        //                 ViewModel.FilteredItems.RemoveAt(i);
        //             }
        //             break;
        //         }
        //     }

        //     if (ViewModel.FilteredItems[i] != newMatches[i])
        //     {
        //         ViewModel.FilteredItems.Insert(i, newMatches[i]);
        //     }
        // }

        // // Remove any extra trailing items from the destination
        // while (ViewModel.FilteredItems.Count > newMatches.Count)
        // {
        //     ViewModel.FilteredItems.RemoveAt(ViewModel.FilteredItems.Count - 1);//RemoveAtEnd
        // }

        // // Add any extra trailing items from the source
        // while (ViewModel.FilteredItems.Count < newMatches.Count)
        // {
        //     ViewModel.FilteredItems.Add(newMatches[ViewModel.FilteredItems.Count]);
        // }
        */

        // set the selected index to the first item in the list
        if (ItemsList.Items.Count > 0)
        {
            ItemsList.SelectedIndex = 0;
            ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }
    }

    private void BackButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel?.GoBack();
    }
}
