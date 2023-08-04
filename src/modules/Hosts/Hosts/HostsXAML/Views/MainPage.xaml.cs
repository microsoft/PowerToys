// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Hosts.Models;
using Hosts.Settings;
using Hosts.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Hosts.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; private set; }

        public ICommand NewDialogCommand => new AsyncRelayCommand(OpenNewDialogAsync);

        public ICommand AdditionalLinesDialogCommand => new AsyncRelayCommand(OpenAdditionalLinesDialogAsync);

        public ICommand AddCommand => new RelayCommand(Add);

        public ICommand UpdateCommand => new RelayCommand(Update);

        public ICommand DeleteCommand => new RelayCommand(Delete);

        public ICommand UpdateAdditionalLinesCommand => new RelayCommand(UpdateAdditionalLines);

        public ICommand ExitCommand => new RelayCommand(() => { Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(Application.Current.Exit); });

        public MainPage()
        {
            InitializeComponent();
            ViewModel = App.GetService<MainViewModel>();
            DataContext = ViewModel;
        }

        private async Task OpenNewDialogAsync()
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            EntryDialog.Title = resourceLoader.GetString("AddNewEntryDialog_Title");
            EntryDialog.PrimaryButtonText = resourceLoader.GetString("AddBtn");
            EntryDialog.PrimaryButtonCommand = AddCommand;
            EntryDialog.DataContext = new Entry(ViewModel.NextId, string.Empty, string.Empty, string.Empty, true);
            await EntryDialog.ShowAsync();
        }

        private async Task OpenAdditionalLinesDialogAsync()
        {
            AdditionalLines.Text = ViewModel.AdditionalLines;
            await AdditionalLinesDialog.ShowAsync();
        }

        private async void Entries_ItemClick(object sender, ItemClickEventArgs e)
        {
            await ShowEditDialogAsync(e.ClickedItem as Entry);
        }

        public async Task ShowEditDialogAsync(Entry entry)
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            ViewModel.Selected = entry;
            EntryDialog.Title = resourceLoader.GetString("UpdateEntry_Title");
            EntryDialog.PrimaryButtonText = resourceLoader.GetString("UpdateBtn");
            EntryDialog.PrimaryButtonCommand = UpdateCommand;
            var clone = ViewModel.Selected.Clone();
            EntryDialog.DataContext = clone;
            await EntryDialog.ShowAsync();
        }

        private void Add()
        {
            ViewModel.Add(EntryDialog.DataContext as Entry);
        }

        private void Update()
        {
            ViewModel.Update(Entries.SelectedIndex, EntryDialog.DataContext as Entry);
        }

        private void Delete()
        {
            ViewModel.DeleteSelected();
        }

        private void UpdateAdditionalLines()
        {
            ViewModel.UpdateAdditionalLines(AdditionalLines.Text);
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
                    ShowMode = FlyoutShowMode.Transient, // https://github.com/microsoft/PowerToys/issues/21263
                });
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var menuFlyoutItem = sender as MenuFlyoutItem;

            if (menuFlyoutItem != null)
            {
                await ShowDeleteDialogAsync(menuFlyoutItem.DataContext as Entry);
            }
        }

        public async Task ShowDeleteDialogAsync(Entry entry)
        {
            ViewModel.Selected = entry;
            DeleteDialog.Title = entry.Address;
            await DeleteDialog.ShowAsync();
        }

        private async void Ping_Click(object sender, RoutedEventArgs e)
        {
            var menuFlyoutItem = sender as MenuFlyoutItem;

            if (menuFlyoutItem != null)
            {
                await PingAsync(menuFlyoutItem.DataContext as Entry);
            }
        }

        private async Task PingAsync(Entry entry)
        {
            ViewModel.Selected = entry;
            await ViewModel.PingSelectedAsync();
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            var menuFlyoutItem = sender as MenuFlyoutItem;

            if (menuFlyoutItem != null)
            {
                await ShowEditDialogAsync(menuFlyoutItem.DataContext as Entry);
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var userSettings = App.GetService<IUserSettings>();
            if (userSettings.ShowStartupWarning)
            {
                var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
                var dialog = new ContentDialog();

                dialog.XamlRoot = XamlRoot;
                dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                dialog.Title = resourceLoader.GetString("WarningDialog_Title");
                dialog.Content = new TextBlock
                {
                    Text = resourceLoader.GetString("WarningDialog_Text"),
                    TextWrapping = TextWrapping.Wrap,
                };
                dialog.PrimaryButtonText = resourceLoader.GetString("WarningDialog_AcceptBtn");
                dialog.PrimaryButtonStyle = Application.Current.Resources["AccentButtonStyle"] as Style;
                dialog.CloseButtonText = resourceLoader.GetString("WarningDialog_QuitBtn");
                dialog.CloseButtonCommand = ExitCommand;

                await dialog.ShowAsync();
            }
        }

        private void ReorderButtonUp_Click(object sender, RoutedEventArgs e)
        {
            var menuFlyoutItem = sender as MenuFlyoutItem;

            if (menuFlyoutItem != null)
            {
                var entry = menuFlyoutItem.DataContext as Entry;
                var index = ViewModel.Entries.IndexOf(entry);
                if (index > 0)
                {
                    ViewModel.Move(index, index - 1);
                }
            }
        }

        private void ReorderButtonDown_Click(object sender, RoutedEventArgs e)
        {
            var menuFlyoutItem = sender as MenuFlyoutItem;

            if (menuFlyoutItem != null)
            {
                var entry = menuFlyoutItem.DataContext as Entry;
                var index = ViewModel.Entries.IndexOf(entry);
                if (index < ViewModel.Entries.Count - 1)
                {
                    ViewModel.Move(index, index + 1);
                }
            }
        }

        /// <summary>
        /// Handle the keyboard shortcuts at list view level since
        /// KeyboardAccelerators in FlyoutBase.AttachedFlyout works only when the flyout is open
        /// </summary>
        private async void Entries_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView != null && e.KeyStatus.WasKeyDown == false)
            {
                var entry = listView.SelectedItem as Entry;

                if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
                {
                    if (e.Key == VirtualKey.E)
                    {
                        await ShowEditDialogAsync(entry);
                    }
                    else if (e.Key == VirtualKey.P)
                    {
                        await PingAsync(entry);
                    }
                }
                else if (e.Key == VirtualKey.Delete)
                {
                    await ShowDeleteDialogAsync(entry);
                }
            }
        }

        /// <summary>
        /// Focus the first item when the list view gets the focus with keyboard
        /// </summary>
        private void Entries_GotFocus(object sender, RoutedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView.SelectedItem == null && listView.Items.Count > 0)
            {
                listView.SelectedIndex = 0;
            }
        }

        private void MenuFlyout_Opened(object sender, object e)
        {
            // Focus the first item: required for workaround https://github.com/microsoft/PowerToys/issues/21263
            var menuFlyout = sender as MenuFlyout;
            if (menuFlyout != null && menuFlyout.Items.Count > 0)
            {
                menuFlyout.Items.First().Focus(FocusState.Programmatic);
            }
        }
    }
}
