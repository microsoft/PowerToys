// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

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
