// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ProjectsEditor.ViewModels;

namespace ProjectsEditor
{
    /// <summary>
    /// Interaction logic for SnapshotWindow.xaml
    /// </summary>
    public partial class SnapshotWindow : Window
    {
        private MainViewModel _mainViewModel;

        public SnapshotWindow(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            InitializeComponent();
        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            Close();
            _mainViewModel.CancelSnapshot();
        }

        private void SnapshotButtonClicked(object sender, RoutedEventArgs e)
        {
            _mainViewModel.AddNewProject();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _mainViewModel.CancelSnapshot();
        }
    }
}
