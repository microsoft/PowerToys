// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Hosts.Helpers;
using Hosts.Models;
using Hosts.Settings;
using Hosts.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.Resources;

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
            var resourceLoader = ResourceLoader.GetForViewIndependentUse();
            EntryDialog.Title = resourceLoader.GetString("AddNewEntryDialog_Title");
            EntryDialog.PrimaryButtonText = resourceLoader.GetString("AddBtn");
            EntryDialog.PrimaryButtonCommand = AddCommand;
            EntryDialog.DataContext = new Entry(string.Empty, string.Empty, string.Empty, true);
            await EntryDialog.ShowAsync();
        }

        private async Task OpenAdditionalLinesDialogAsync()
        {
            AdditionalLines.Text = ViewModel.AdditionalLines;
            await AdditionalLinesDialog.ShowAsync();
        }

        private async void Entries_ItemClick(object sender, ItemClickEventArgs e)
        {
            var resourceLoader = ResourceLoader.GetForViewIndependentUse();
            ViewModel.Selected = e.ClickedItem as Entry;
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
                var selectedEntry = menuFlyoutItem.DataContext as Entry;
                ViewModel.Selected = selectedEntry;
                DeleteDialog.Title = selectedEntry.Address;
                await DeleteDialog.ShowAsync();
            }
        }

        private async void Ping_Click(object sender, RoutedEventArgs e)
        {
            var menuFlyoutItem = sender as MenuFlyoutItem;

            if (menuFlyoutItem != null)
            {
                ViewModel.Selected = menuFlyoutItem.DataContext as Entry;
                await ViewModel.PingSelectedAsync();
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var userSettings = App.GetService<IUserSettings>();
            if (userSettings.ShowStartupWarning)
            {
                var resourceLoader = ResourceLoader.GetForViewIndependentUse();
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
                    ViewModel.Entries.Move(index, index - 1);
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
                    ViewModel.Entries.Move(index, index + 1);
                }
            }
        }

        private void ContentDialog_Loaded_ApplyMargin(object sender, RoutedEventArgs e)
        {
            try
            {
                // Based on the template from dev/CommonStyles/ContentDialog_themeresources.xaml in https://github.com/microsoft/microsoft-ui-xaml
                var border = Helpers.VisualTreeUtils.FindVisualChildByName(sender as ContentDialog, "BackgroundElement") as Border;
                border.Margin = new Thickness(0, 32, 0, 0); // Should be the size reserved for the title bar as in MainWindow.xaml
            }
            catch (Exception ex)
            {
                Logger.LogError("Couldn't set the margin for a content dialog. It will appear on top of the title bar.", ex);
            }
        }
    }
}
