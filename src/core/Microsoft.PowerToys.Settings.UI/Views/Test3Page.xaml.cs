using System;

using Microsoft.PowerToys.Settings.UI.ViewModels;

using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class Test3Page : Page
    {
        public Test3ViewModel ViewModel { get; } = new Test3ViewModel();

        public Test3Page()
        {
            InitializeComponent();
        }
    }
}
