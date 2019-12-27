using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using ImageResizer.ViewModels;

namespace ImageResizer.Views
{
    public partial class AdvancedWindow : Window
    {
        public AdvancedWindow(AdvancedViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }

        void HandleAcceptClick(object sender, RoutedEventArgs e)
            => DialogResult = true;

        void HandleRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.ToString());
            e.Handled = true;
        }
    }
}
