using System;

using PowerToys_Settings_Sandbox.ViewModels;

using Windows.UI.Xaml.Controls;

namespace PowerToys_Settings_Sandbox.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();

        public MainPage()
        {
            InitializeComponent();
        }
    }
}
