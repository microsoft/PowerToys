// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using HostsUILib.Helpers;
using HostsUILib.Models;
using HostsUILib.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace HostsUILib.Views
{
    public partial class HostsMainPage : Page
    {
        public MainViewModel ViewModel { get; private set; }

        public ICommand NewDialogCommand => new AsyncRelayCommand(OpenNewDialogAsync);

        public ICommand AdditionalLinesDialogCommand => new AsyncRelayCommand(OpenAdditionalLinesDialogAsync);

        public ICommand AddCommand => new RelayCommand(Add);

        public ICommand UpdateCommand => new RelayCommand(Update);

        public ICommand DeleteCommand => new RelayCommand(Delete);

        public ICommand UpdateAdditionalLinesCommand => new RelayCommand(UpdateAdditionalLines);

        public ICommand ExitCommand => new RelayCommand(() => { Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(Application.Current.Exit); });

        public HostsMainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;

            DataContext = ViewModel;
        }

        private async Task OpenNewDialogAsync()
        {
            await ShowAddDialogAsync();
        }

        private async Task OpenAdditionalLinesDialogAsync()
        {
            AdditionalLines.Text = ViewModel.AdditionalLines;
            await AdditionalLinesDialog.ShowAsync();
        }

        private async void Entries_ItemClick(object sender, ItemClickEventArgs e)
        {
            Entry entry = e.ClickedItem as Entry;
            ViewModel.Selected = entry;
            await ShowEditDialogAsync(entry);
        }

        public async Task ShowEditDialogAsync(Entry entry)
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
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

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (Entries.SelectedItem is Entry entry)
            {
                ViewModel.Selected = entry;
                DeleteDialog.Title = entry.Address;
                await DeleteDialog.ShowAsync();
            }
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (Entries.SelectedItem is Entry entry)
            {
                await ShowEditDialogAsync(entry);
            }
        }

        private async void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            if (Entries.SelectedItem is Entry entry)
            {
                await ShowAddDialogAsync(entry);
            }
        }

        private async void Ping_Click(object sender, RoutedEventArgs e)
        {
            if (Entries.SelectedItem is Entry entry)
            {
                ViewModel.Selected = entry;
                await ViewModel.PingSelectedAsync();
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.ReadHosts();

            var userSettings = ViewModel.UserSettings;
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
            if (Entries.SelectedItem is Entry entry)
            {
                var index = ViewModel.Entries.IndexOf(entry);
                if (index > 0)
                {
                    ViewModel.Move(index, index - 1);
                }
            }
        }

        private void ReorderButtonDown_Click(object sender, RoutedEventArgs e)
        {
            if (Entries.SelectedItem is Entry entry)
            {
                var index = ViewModel.Entries.IndexOf(entry);
                if (index < ViewModel.Entries.Count - 1)
                {
                    ViewModel.Move(index, index + 1);
                }
            }
        }

        /// <summary>
        /// Focus the first item when the list view gets the focus with keyboard
        /// </summary>
        private void Entries_GotFocus(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var entry = element.DataContext as Entry;

            if (entry != null)
            {
                ViewModel.Selected = entry;
            }
            else if (Entries.SelectedItem == null && Entries.Items.Count > 0)
            {
                Entries.SelectedIndex = 0;
            }
        }

        private void Entries_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var entry = (e.OriginalSource as FrameworkElement).DataContext as Entry;
            ViewModel.Selected = entry;
        }

        private async Task ShowAddDialogAsync(Entry template = null)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            EntryDialog.Title = resourceLoader.GetString("AddNewEntryDialog_Title");
            EntryDialog.PrimaryButtonText = resourceLoader.GetString("AddBtn");
            EntryDialog.PrimaryButtonCommand = AddCommand;

            EntryDialog.DataContext = template == null
                ? new Entry(ViewModel.NextId, string.Empty, string.Empty, string.Empty, true)
                : new Entry(ViewModel.NextId, template.Address, template.Hosts, template.Comment, template.Active);

            await EntryDialog.ShowAsync();
        }

        private void ContentDialog_Loaded_ApplyMargin(object sender, RoutedEventArgs e)
        {
            try
            {
                // Based on the template from dev/CommonStyles/ContentDialog_themeresources.xaml in https://github.com/microsoft/microsoft-ui-xaml
                var border = VisualTreeUtils.FindVisualChildByName(sender as ContentDialog, "BackgroundElement") as Border;
                if (border is not null)
                {
                    border.Margin = new Thickness(0, 32, 0, 0); // Should be the size reserved for the title bar as in MainWindow.
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Logger.LogError("Couldn't set the margin for a content dialog. It will appear on top of the title bar.", ex);
            }
        }
    }
}
