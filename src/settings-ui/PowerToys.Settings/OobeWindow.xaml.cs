// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;

namespace PowerToys.Settings
{
    /// <summary>
    /// Interaction logic for OobeWindow.xaml
    /// </summary>
    public partial class OobeWindow : Window
    {
        private static Window inst;

        public OobeWindow()
        {
            InitializeComponent();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            inst = null;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (inst != null)
            {
                inst.Close();
            }

            inst = this;
        }
    }
}
