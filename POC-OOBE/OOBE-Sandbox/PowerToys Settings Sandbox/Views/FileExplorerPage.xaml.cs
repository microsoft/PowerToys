using System;

using PowerToys_Settings_Sandbox.ViewModels;

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace PowerToys_Settings_Sandbox.Views
{
    public sealed partial class FileExplorerPage : Page
    {
        public MasterDetailViewModel ViewModel { get; } = new MasterDetailViewModel();

        public FileExplorerPage()
        {
            InitializeComponent();
            Loaded += FileExplorerPage_Loaded;
        }

        private async void FileExplorerPage_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadDataAsync(MasterDetailsViewControl.ViewState);
        }
    }
}
