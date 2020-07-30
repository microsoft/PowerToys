// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;
using System.Windows;
using ColorPicker.ViewModelContracts;

namespace ColorPicker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Closing += MainWindow_Closing;
            Bootstrapper.InitializeContainer(this);
            InitializeComponent();
            DataContext = this;
            Hide();
        }

        [Import]
        public IMainViewModel MainViewModel { get; set; }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Closing -= MainWindow_Closing;
            Bootstrapper.Dispose();
        }
    }
}
