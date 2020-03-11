using System;

using Microsoft.PowerToys.Settings.UI.ViewModels;

using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class Test1Page : Page
    {
        public Test1ViewModel ViewModel { get; } = new Test1ViewModel();

        public Test1Page()
        {
            InitializeComponent();
        }
    }
}
