using Microsoft.PowerToys.Settings.UI.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ColorPickerPage : Page
    {
        public ColorPickerViewModel ViewModel { get; set; }

        public ColorPickerPage()
        {
            ViewModel = new ColorPickerViewModel();
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
