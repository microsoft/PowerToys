// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using DeveloperCommandPalette;
using Microsoft.CmdPal.Extensions;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace WindowsCommandPalette.Views;

public sealed partial class MarkdownPage : Page, INotifyPropertyChanged
{
    public MarkdownPageViewModel? ViewModel { get; set; }

#pragma warning disable CS0067
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore

    public MarkdownPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = (MarkdownPageViewModel?)e.Parameter;

        if (ViewModel == null)
        {
            return;
        }

        ViewModel.InitialRender(this).ContinueWith((t) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.MarkdownContent.Length > 0)
                {
                    // TODO! We're only rendering the first body for now, but we can totally support multiple
                    mdTextBox.Text = ViewModel.MarkdownContent[0];
                    TitleBlock.Text = ViewModel.Title;
                }

                // if (ViewModel.PageCommand != null)
                // {
                //            ActionsDropdown.ItemsSource = ViewModel.PageCommand.Commands;
                //            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MoreCommandsAvailable)));
                //            // this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItemDefaultAction)));
                // }
            });
        });
    }

    private void DoAction(ActionViewModel actionViewModel)
    {
        ViewModel?.DoAction(actionViewModel);
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    private void MarkdownScrollViewer_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        const double ScrollAmount = 50.0;

        if (e.Key == Windows.System.VirtualKey.Up)
        {
            MarkdownScrollViewer.ChangeView(null, MarkdownScrollViewer.VerticalOffset - ScrollAmount, null);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Down)
        {
            MarkdownScrollViewer.ChangeView(null, MarkdownScrollViewer.VerticalOffset + ScrollAmount, null);
            e.Handled = true;
        }
    }

    private void BackButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel?.GoBack();
    }

    private void MoreCommandsButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        FlyoutShowOptions options = new FlyoutShowOptions
        {
            ShowMode = FlyoutShowMode.Standard,
        };
        MoreCommandsButton.Flyout.ShowAt(MoreCommandsButton, options);

        // ActionsDropdown.SelectedIndex = 0;
        // ActionsDropdown.Focus(FocusState.Programmatic);
    }

#pragma warning disable CA1822 // Mark members as static
    private bool MoreCommandsAvailable => (ViewModel?.Page.Commands != null) && (ViewModel.Page.Commands.Length > 0);

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

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        var ctrlPressed = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (ctrlPressed && e.Key == Windows.System.VirtualKey.K)
        {
            // Open the more actions flyout and focus the first item
            if (ActionsDropdown.Items.Count > 0)
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
    }
}
