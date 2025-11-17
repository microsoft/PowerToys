// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for LayoutOverlayWindow.xaml
    /// </summary>
    public partial class LayoutOverlayWindow : Window
    {
        public LayoutOverlayWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utils.NativeMethods.SetWindowStyleToolWindow(this);
        }
    }
}
