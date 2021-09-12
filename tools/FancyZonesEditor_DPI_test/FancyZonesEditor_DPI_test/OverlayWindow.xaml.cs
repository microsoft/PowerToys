using System;
using System.Windows;
using System.Windows.Media;

namespace FancyZonesEditor_DPI_test
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            InitializeComponent();
        }

        private void Window_DpiChanged(object sender, DpiChangedEventArgs e)
        {
        }
    }

}
