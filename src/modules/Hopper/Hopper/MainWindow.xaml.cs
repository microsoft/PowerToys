// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Hopper;

namespace PowerToys.Hopper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(string[] files)
        {
            Debugger.Launch();
            Debugger.Break();

            string[] file2 = Array.Empty<string>();

            foreach (var file in files)
            {
                if (file.TrimStart() != "\0")
                {
                    file2 = file2.Append(file.Replace("\0", string.Empty)).ToArray<string>();
                }
            }

            InitializeComponent();
            FileList.ItemsSource = file2;
        }
    }
}
