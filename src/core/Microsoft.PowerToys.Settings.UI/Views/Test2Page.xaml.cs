using System;

using Microsoft.PowerToys.Settings.UI.ViewModels;

using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class Test2Page : Page
    {
        public Test2ViewModel ViewModel { get; } = new Test2ViewModel();

        public Test2Page()
        {
            InitializeComponent();
        }
    }
}
