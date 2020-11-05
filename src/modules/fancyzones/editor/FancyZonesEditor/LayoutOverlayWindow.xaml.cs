// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Media;

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

        private void Window_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            VisualTreeHelper.SetRootDpi(this, e.NewDpi);
        }
    }
}
