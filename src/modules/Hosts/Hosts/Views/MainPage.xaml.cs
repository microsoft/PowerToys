// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Hosts.Models;
using Hosts.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace Hosts.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; private set; }

        public ICommand NewDialogCommand => new AsyncRelayCommand(OpenNewDialogAsync);

        public ICommand AddCommand => new RelayCommand(Add);

        public ICommand UpdateCommand => new RelayCommand(Update);

        public ICommand DeleteCommand => new RelayCommand(Delete);

        private async Task OpenNewDialogAsync()
        {
            MainDialog.Title = "Add entry";
            MainDialog.PrimaryButtonText = "Add";
            MainDialog.PrimaryButtonCommand = AddCommand;
            MainDialog.DataContext = new Entry(string.Empty, string.Empty, string.Empty, true);
            await MainDialog.ShowAsync();
        }

        private async void Entries_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Selected = e.ClickedItem as Entry;
            MainDialog.Title = "Update entry";
            MainDialog.PrimaryButtonText = "Update";
            MainDialog.PrimaryButtonCommand = UpdateCommand;
            var clone = ViewModel.Selected.Clone();
            MainDialog.DataContext = clone;
            await MainDialog.ShowAsync();
        }

        public MainPage()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;
        }

        public void Add()
        {
            ViewModel.Add(MainDialog.DataContext as Entry);
        }

        public void Update()
        {
            ViewModel.Update(Entries.SelectedIndex, MainDialog.DataContext as Entry);
        }

        public void Delete()
        {
            ViewModel.DeleteSelected();
        }

        private void Grid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var owner = sender as FrameworkElement;
            if (owner != null)
            {
                var flyoutBase = FlyoutBase.GetAttachedFlyout(owner);
                flyoutBase.ShowAt(owner, new FlyoutShowOptions
                {
                    Position = e.GetPosition(owner),
                });
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var menuFlyoutItem = sender as MenuFlyoutItem;

            if (menuFlyoutItem != null)
            {
                ViewModel.Selected = menuFlyoutItem.DataContext as Entry;
                await DeleteDialog.ShowAsync();
            }
        }

        private void Enable_Click(object sender, RoutedEventArgs e)
        {
            var menuFlyoutItem = sender as MenuFlyoutItem;

            if (menuFlyoutItem != null)
            {
                ViewModel.Selected = menuFlyoutItem.DataContext as Entry;
                ViewModel.EnableSelected();
            }
        }

        private void Disable_Click(object sender, RoutedEventArgs e)
        {
            var menuFlyoutItem = sender as MenuFlyoutItem;

            if (menuFlyoutItem != null)
            {
                ViewModel.Selected = menuFlyoutItem.DataContext as Entry;
                ViewModel.DisableSelected();
            }
        }
    }
}
