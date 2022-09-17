// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace ColorPicker
{
    /// <summary>
    /// Interaction logic for ZoomWindow.xaml
    /// </summary>
    public partial class ZoomWindow : Window
    {
        public ZoomWindow()
        {
            InitializeComponent();
            DataContext = this;

            // must be large enough to fit max zoom
            Width = 500;
            Height = 500;
        }
    }
}
